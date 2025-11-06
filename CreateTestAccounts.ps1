# PowerShell script to create test accounts
# Run this script to add test accounts to accounts.json

$accountsPath = "D:\PRN212\PRN212_FlappyBird_Project\PRN212.G5.FlappyBird\bin\Debug\net9.0-windows\accounts.json"

Write-Host "=== Create Test Accounts ===" -ForegroundColor Cyan
Write-Host ""

# Read existing accounts
$accounts = @()
if (Test-Path $accountsPath) {
    $accounts = Get-Content $accountsPath | ConvertFrom-Json
    Write-Host "Found $($accounts.Count) existing account(s)" -ForegroundColor Yellow
}

# Tạo tài khoản test
$testAccounts = @(
    @{
        Email = "admin@flappybird.com"
        Password = "admin123"
        Name = "Administrator"
        Avatar = ""
    },
    @{
        Email = "test@example.com"
        Password = "123456"
        Name = "Test User"
        Avatar = ""
    }
)

foreach ($testAccount in $testAccounts) {
    # Check if email already exists
    $exists = $accounts | Where-Object { $_.Email -eq $testAccount.Email }
    
    if ($exists) {
        Write-Host "⚠️  Account $($testAccount.Email) already exists!" -ForegroundColor Yellow
    } else {
        Write-Host "Creating account: $($testAccount.Email)..." -ForegroundColor Green
        
        # Hash password (SHA256)
        $passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($testAccount.Password)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $hashBytes = $sha256.ComputeHash($passwordBytes)
        $hashedPassword = [Convert]::ToBase64String($hashBytes)
        
        # Tạo account object
        $account = [PSCustomObject]@{
            Email = $testAccount.Email
            Password = $hashedPassword
            Name = $testAccount.Name
            Avatar = $testAccount.Avatar
            CreatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz")
            HighScore = 0
        }
        
        $accounts += $account
        Write-Host "✅ Created account: $($testAccount.Email) / Password: $($testAccount.Password)" -ForegroundColor Green
    }
}

# Save accounts
$accounts | ConvertTo-Json -Depth 10 | Set-Content $accountsPath -Encoding UTF8

Write-Host ""
Write-Host "=== Completed ===" -ForegroundColor Cyan
Write-Host "File accounts.json has been updated at:" -ForegroundColor White
Write-Host $accountsPath -ForegroundColor Gray
Write-Host ""
Write-Host "Test accounts:" -ForegroundColor Yellow
Write-Host "1. Email: admin@flappybird.com / Password: admin123" -ForegroundColor White
Write-Host "2. Email: test@example.com / Password: 123456" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

