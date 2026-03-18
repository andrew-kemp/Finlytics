namespace FinanceHubFunctions.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PersonalEmail { get; set; }
        public string? NationalInsuranceNumber { get; set; }
        public string? TaxCode { get; set; }
        public decimal? AnnualSalary { get; set; }
        public string? PaymentSchedule { get; set; } // Monthly, Weekly, Bi-weekly
        public string? StartDate { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankSortCode { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public bool IsDirector { get; set; }  // Mark employee as a company director
        public string? Notes { get; set; }
    }
}
