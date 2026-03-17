UPDATE Customers SET Code = 'ESK001', CustomerCode = 'ESK001' 
WHERE Name LIKE 'Esken%' AND (Code IS NULL OR Code = '');

UPDATE Customers SET Code = 'ESK002', CustomerCode = 'ESK002' 
WHERE Name LIKE 'EskenMill%';

UPDATE Customers SET Code = 'ESK003', CustomerCode = 'ESK003' 
WHERE Name LIKE 'EskenMiller%';

SELECT Id, Code, CustomerCode, Name FROM Customers ORDER BY Name;
