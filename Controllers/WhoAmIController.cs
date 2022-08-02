using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Security.Claims;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Azure.SQL.DB.Samples.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WhoAmIController : ControllerBase
    {
        private readonly string _sql = @"
            select 
                json_query((
                    select 
                        * 
                    from (
                        values 
                            (session_user, user_name(), suser_name())
                        ) T 
                            ([session_user], [user_name], [suser_name]) 
                    for json auto
                    )) as userData                
            from
                (values(1)) t(c) for json auto
        ";

        private readonly ILogger<WhoAmIController> _logger;
        private readonly IConfiguration _config;

        public WhoAmIController(IConfiguration config, ILogger<WhoAmIController> logger)
        {
            _logger = logger;
            _config = config;
        }

        private async Task<IActionResult> RunQuery(Func<SqlConnection, Task<JsonElement>> commandAsync, string token = null)
        {
            try
            {
                var csb = new SqlConnectionStringBuilder(_config.GetConnectionString("AzureSQL"));                
                using (var conn = new SqlConnection(csb.ConnectionString))
                {                    
                    if (string.IsNullOrEmpty(csb.UserID)) {
                        if (string.IsNullOrEmpty(token)) {
                            var credential = new Azure.Identity.DefaultAzureCredential();
                            var accessToken = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }));
                            conn.AccessToken = accessToken.Token;
                        } else {
                            conn.AccessToken = token;
                        }
                    }
                    await conn.OpenAsync();
                    JsonElement qr = await commandAsync(conn);
                    conn.Close();

                    return Ok(qr);
                }
            }
            catch (Exception e)
            {                
                string message = string.Empty;
                var ie = e;
                while (ie != null)
                {
                    message += ie.Message + "; ";
                    ie = ie.InnerException;
                }                
                return BadRequest(message);
            }
        }

        [HttpGet]        
        public async Task<IActionResult> Default()
        {
            return await RunQuery(async (conn) => {
                var qr = await conn.QuerySingleOrDefaultAsync<string>(_sql);
                return JsonDocument.Parse(qr).RootElement;
            });            
        }

        // Needed: grant impersonate on user::[web_user] to database-mapped MSI
        [HttpGet]
        [Route("impersonate")]
        public async Task<IActionResult> Impersonate()
        {
            return await RunQuery(async (conn) => {
                var revertCookie = await conn.ExecuteScalarAsync<Byte[]>("declare @c varbinary(8000); execute as user = 'web_user' with cookie into @c; select @c;");
                var qr = await conn.QuerySingleOrDefaultAsync<string>(_sql);
                await conn.ExecuteAsync("revert with cookie = @c", new { @c = revertCookie });
                return JsonDocument.Parse(qr).RootElement;
            });            
        }

        [HttpGet]
        [Route("token")]
        public async Task<IActionResult> Token()
        {
            string token = string.Empty;
            string authHeader = HttpContext.Request.Headers?["Authorization"];
            if (!string.IsNullOrEmpty(authHeader)) {
                var authHeaderTokens = authHeader.Split(' ');
                if (authHeaderTokens.Count() == 2 && authHeaderTokens[0].Trim().ToLower() == "bearer") 
                    token = authHeaderTokens[1].Trim();                                
            }
            return await RunQuery(async (conn) => {                
                var qr = await conn.QuerySingleOrDefaultAsync<string>(_sql);
                return JsonDocument.Parse(qr).RootElement;
            }, token);            
        }
    }
}