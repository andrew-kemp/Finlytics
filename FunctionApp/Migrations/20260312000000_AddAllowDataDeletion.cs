using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowDataDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanySettings') AND name = 'AllowDataDeletion')
                    ALTER TABLE [CompanySettings] ADD [AllowDataDeletion] bit NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanySettings') AND name = 'AllowDataDeletion')
                    ALTER TABLE [CompanySettings] DROP COLUMN [AllowDataDeletion];
            ");
        }
    }
}
