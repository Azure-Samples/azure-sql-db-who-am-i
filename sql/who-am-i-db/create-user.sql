-- This file contains SQL statements that will be executed before the build script.

if (user_id('dm-who-am-i') is null) 
    exec('create user [dm-who-am-i] from external provider')
go

if (is_rolemember('db_owner', 'dm-who-am-i') = 0)
    alter role db_owner add member [dm-who-am-i]
go