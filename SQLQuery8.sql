SELECT 
    Username,
    LEN(Username) AS UserLen,
    Password,
    LEN(Password) AS PassLen
FROM Users
WHERE Username LIKE 'F123456789%'; 