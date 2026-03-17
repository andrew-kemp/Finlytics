using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net;
using FinanceHubFunctions.Data;

namespace FinanceHubFunctions.Functions
{
    public class DatabaseMigrationFunctions
    {
        private readonly ILogger<DatabaseMigrationFunctions> _logger;
        private readonly FinanceHubDbContext _dbContext;

        public DatabaseMigrationFunctions(ILogger<DatabaseMigrationFunctions> logger, FinanceHubDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [Function("AddIsDirectorColumn")]
        public async Task<HttpResponseData> AddIsDirectorColumn([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Starting AddIsDirectorColumn migration");

                // Check if column already exists
                var checkColumnSql = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'IsDirector'";
                
                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = checkColumnSql;
                await _dbContext.Database.OpenConnectionAsync();
                var result = await command.ExecuteScalarAsync();
                var columnExists = Convert.ToInt32(result) > 0;
                
                if (columnExists)
                {
                    _logger.LogInformation("IsDirector column already exists in Employees table");
                    
                    var response1 = req.CreateResponse(HttpStatusCode.OK);
                    await response1.WriteAsJsonAsync(new { 
                        success = true, 
                        message = "IsDirector column already exists in Employees table" 
                    });
                    return response1;
                }

                // Column doesn't exist, add it
                var addColumnSql = "ALTER TABLE Employees ADD IsDirector BIT NOT NULL DEFAULT 0";
                await _dbContext.Database.ExecuteSqlRawAsync(addColumnSql);

                _logger.LogInformation("Successfully added IsDirector column to Employees table");
                
                var response2 = req.CreateResponse(HttpStatusCode.OK);
                await response2.WriteAsJsonAsync(new { 
                    success = true, 
                    message = "Added IsDirector column to Employees table successfully" 
                });
                return response2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding IsDirector column to Employees table");
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { 
                    success = false, 
                    error = ex.Message 
                });
                return response;
            }
        }

        [Function("MarkEmployeesAsDirectors")]
        public async Task<HttpResponseData> MarkEmployeesAsDirectors([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Starting MarkEmployeesAsDirectors");

                // Mark Andy Kemp as director (adjust the name as needed)
                var updateSql = "UPDATE Employees SET IsDirector = 1 WHERE Name IN ('Andy Kemp', 'Andrew Kemp')";
                var rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(updateSql);

                _logger.LogInformation($"Marked {rowsAffected} employees as directors");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { 
                    success = true, 
                    message = $"Marked {rowsAffected} employees as directors",
                    rowsAffected = rowsAffected
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employees as directors");
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { 
                    success = false, 
                    error = ex.Message 
                });
                return response;
            }
        }

        [Function("CreateTestEmployee")]
        public async Task<HttpResponseData> CreateTestEmployee([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                // Insert test employee directly via SQL
                var insertEmployeeSql = @"
                    IF NOT EXISTS (SELECT 1 FROM Employees WHERE Name = 'Andrew Kemp')
                    BEGIN
                        INSERT INTO Employees (EmployeeNumber, Name, Email, PhoneNumber, Address, AnnualSalary, IsActive, IsDirector, PaymentSchedule, StartDate, Notes)
                        VALUES ('EMP001', 'Andrew Kemp', 'andy@kemponline.com', '07800503882', 'Edinburgh', 50000, 1, 1, 'Monthly', '2024-01-01', 'Managing Director')
                    END";
                    
                await _dbContext.Database.ExecuteSqlRawAsync(insertEmployeeSql);

                _logger.LogInformation("Successfully created test employee Andrew Kemp");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { 
                    success = true, 
                    message = "Created test employee Andrew Kemp successfully" 
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test employee");
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { 
                    success = false, 
                    error = ex.Message 
                });
                return response;
            }
        }

        [Function("AddPayrollAutoScheduleColumns")]
        public async Task<HttpResponseData> AddPayrollAutoScheduleColumns(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                await _dbContext.Database.OpenConnectionAsync();
                var conn = _dbContext.Database.GetDbConnection();

                async Task<bool> ColumnExists(string table, string column)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@t"; p1.Value = table; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@c"; p2.Value = column; cmd.Parameters.Add(p2);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }

                var added = new List<string>();

                if (!await ColumnExists("PayrollSettings", "AutoRunEnabled"))
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE PayrollSettings ADD AutoRunEnabled BIT NOT NULL DEFAULT 0");
                    added.Add("PayrollSettings.AutoRunEnabled");
                }
                if (!await ColumnExists("PayrollSettings", "AutoRunDaysBefore"))
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE PayrollSettings ADD AutoRunDaysBefore INT NULL");
                    added.Add("PayrollSettings.AutoRunDaysBefore");
                }
                if (!await ColumnExists("PayrollSettings", "AutoPostImmediately"))
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE PayrollSettings ADD AutoPostImmediately BIT NOT NULL DEFAULT 0");
                    added.Add("PayrollSettings.AutoPostImmediately");
                }
                if (!await ColumnExists("PayrollSettings", "AutoRunLastTriggered"))
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE PayrollSettings ADD AutoRunLastTriggered DATETIME2 NULL");
                    added.Add("PayrollSettings.AutoRunLastTriggered");
                }

                _logger.LogInformation("PayrollAutoSchedule migration: added {Count} columns: {Cols}", added.Count, string.Join(", ", added));

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(new { success = true, columnsAdded = added, message = added.Count == 0 ? "All columns already exist" : $"Added {added.Count} column(s)" });
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running payroll auto-schedule migration");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return err;
            }
        }
    }
}