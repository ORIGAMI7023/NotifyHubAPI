# NotifyHub API Attachment Test Script
# Test script for email attachment functionality

$BaseUrl = "https://notify.downf.cn"
$ApiKey = "nh-prod-Y63jT32xDaxWy1EkqiFGPwq7PlzKmaJJak3MgtW26Z8="

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "NotifyHub API Attachment Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: No attachment (backward compatibility)
Write-Host "[Test 1] Send email without attachment" -ForegroundColor Yellow
$body1 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - No Attachment"
    body = "This is a test email without attachment"
    category = "test"
    isHtml = $false
} | ConvertTo-Json

try {
    $response1 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body1 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "PASS - Test 1 - EmailId: $($response1.data.emailId)" -ForegroundColor Green
} catch {
    Write-Host "FAIL - Test 1 - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Single text attachment
Write-Host "[Test 2] Send email with single text attachment" -ForegroundColor Yellow
$textContent = "SGVsbG8gV29ybGQh"
$body2 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Single Attachment"
    body = "Please check the attachment"
    category = "test"
    isHtml = $false
    attachments = @{
        "test.txt" = $textContent
    }
} | ConvertTo-Json -Depth 3

try {
    $response2 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body2 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "PASS - Test 2 - EmailId: $($response2.data.emailId)" -ForegroundColor Green
} catch {
    Write-Host "FAIL - Test 2 - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Multiple attachments
Write-Host "[Test 3] Send email with multiple attachments" -ForegroundColor Yellow
$body3 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Multiple Attachments"
    body = "Multiple attachments test"
    category = "test"
    isHtml = $false
    attachments = @{
        "file1.txt" = "SGVsbG8="
        "file2.txt" = "V29ybGQ="
    }
} | ConvertTo-Json -Depth 3

try {
    $response3 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body3 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "PASS - Test 3 - EmailId: $($response3.data.emailId)" -ForegroundColor Green
} catch {
    Write-Host "FAIL - Test 3 - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Attachment count exceeded (should fail)
Write-Host "[Test 4] Exceeded attachment count (should fail)" -ForegroundColor Yellow
$body4 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Too Many Attachments"
    body = "This should fail"
    category = "test"
    isHtml = $false
    attachments = @{
        "file1.txt" = "SGVsbG8="
        "file2.txt" = "V29ybGQ="
        "file3.txt" = "VGVzdDA="
        "file4.txt" = "VGVzdDE="
        "file5.txt" = "VGVzdDI="
        "file6.txt" = "VGVzdDM="
    }
} | ConvertTo-Json -Depth 3

try {
    $response4 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body4 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "FAIL - Test 4 - Should have failed but passed" -ForegroundColor Red
} catch {
    Write-Host "PASS - Test 4 - Correctly rejected: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 5: Blocked file type (should fail)
Write-Host "[Test 5] Blocked file type (should fail)" -ForegroundColor Yellow
$body5 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Blocked File Type"
    body = "This should fail"
    category = "test"
    isHtml = $false
    attachments = @{
        "test.exe" = "SGVsbG8="
    }
} | ConvertTo-Json -Depth 3

try {
    $response5 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body5 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "FAIL - Test 5 - Should have failed but passed" -ForegroundColor Red
} catch {
    Write-Host "PASS - Test 5 - Correctly rejected: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 6: Invalid Base64 (should fail)
Write-Host "[Test 6] Invalid Base64 encoding (should fail)" -ForegroundColor Yellow
$body6 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Invalid Base64"
    body = "This should fail"
    category = "test"
    isHtml = $false
    attachments = @{
        "test.txt" = "Invalid Base64!!!"
    }
} | ConvertTo-Json -Depth 3

try {
    $response6 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body6 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "FAIL - Test 6 - Should have failed but passed" -ForegroundColor Red
} catch {
    Write-Host "PASS - Test 6 - Correctly rejected: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 7: HTML email with attachment
Write-Host "[Test 7] HTML email with attachment" -ForegroundColor Yellow
$body7 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - HTML with Attachment"
    body = "<h1>Please check the attachment</h1>"
    category = "test"
    isHtml = $true
    attachments = @{
        "test.txt" = "SGVsbG8gV29ybGQh"
    }
} | ConvertTo-Json -Depth 3

try {
    $response7 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body7 -ContentType "application/json" -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Host "PASS - Test 7 - EmailId: $($response7.data.emailId)" -ForegroundColor Green
} catch {
    Write-Host "FAIL - Test 7 - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 8: Chinese filename (UTF-8 encoding)
Write-Host "[Test 8] Attachment with Chinese filename" -ForegroundColor Yellow
$body8 = @{
    to = @("origami7023@gmail.com")
    subject = "Test - Chinese Filename"
    body = "Chinese filename test"
    category = "test"
    isHtml = $false
    attachments = @{
        "测试文件.txt" = "SGVsbG8gV29ybGQh"
    }
} | ConvertTo-Json -Depth 3

try {
    $response8 = Invoke-RestMethod -Uri "$BaseUrl/api/email/send" -Method Post -Body $body8 -ContentType "application/json" -Headers @{ "X-API-Key" = $ApiKey }
    Write-Host "PASS - Test 8 - EmailId: $($response8.data.emailId)" -ForegroundColor Green
} catch {
    Write-Host "FAIL - Test 8 - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
