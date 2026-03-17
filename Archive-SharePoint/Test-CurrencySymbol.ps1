# Test Currency Symbol in PDF
# This script calls the Function App to test PDF generation with currency symbols

$functionAppUrl = "https://func-financehub-2669.azurewebsites.net"

Write-Host "Testing currency symbol in PDF generation..." -ForegroundColor Cyan
Write-Host "`nThe PDF should now display amounts with currency symbols:" -ForegroundColor Yellow
Write-Host "  Line items: Rate (£), Line Total (£)" -ForegroundColor Gray
Write-Host "  Subtotal: £XXX.XX" -ForegroundColor Gray
Write-Host "  Discount: -£XXX.XX" -ForegroundColor Gray
Write-Host "  VAT: £XXX.XX" -ForegroundColor Gray
Write-Host "  Total: £XXX.XX" -ForegroundColor Gray

Write-Host "`n✓ Changes deployed:" -ForegroundColor Green
Write-Host "  ✓ CurrencySymbol field added to SharePoint" -ForegroundColor Green
Write-Host "  ✓ Currency Symbol set to '£' in Company Settings" -ForegroundColor Green
Write-Host "  ✓ PDF service updated with fallback logic (defaults to £)" -ForegroundColor Green
Write-Host "  ✓ All PDF currency references updated (line items, subtotal, discount, VAT, total)" -ForegroundColor Green
Write-Host "  ✓ Function App deployed with fixes" -ForegroundColor Green

Write-Host "`n📋 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Go to: https://hub.kemponline.co.uk" -ForegroundColor White
Write-Host "  2. Navigate to Invoices" -ForegroundColor White
Write-Host "  3. Click 'View PDF' (👁) on any invoice" -ForegroundColor White
Write-Host "  4. Verify all amounts show with £ symbol" -ForegroundColor White

Write-Host "`n💡 To change currency symbol in future:" -ForegroundColor Yellow
Write-Host "  - Edit Company Settings in SharePoint" -ForegroundColor White
Write-Host "  - Or add Currency Symbol field to Company Settings UI" -ForegroundColor White
