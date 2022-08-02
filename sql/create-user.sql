create user [dm-who-am-i] from external provider
go

alter role db_owner add member [dm-who-am-i]
go