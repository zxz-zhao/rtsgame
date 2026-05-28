$log = 'E:\code\c++\UnityRTS\build_apk5.log'
$t = 0
while ($t -lt 360) {
    Start-Sleep 15
    $t += 15
    if (Test-Path $log) {
        $content = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($content) {
            $tail = Get-Content $log -Tail 4 -ErrorAction SilentlyContinue
            $tail | ForEach-Object { Write-Host $_ }
            if ($content -match 'Exiting batchmode|BuildAll|error CS|Fatal|Aborting') {
                $errs = Select-String -Path $log -Pattern 'error CS' | Where-Object { $_ -notmatch 'Licens' }
                if ($errs) { Write-Host "=== COMPILE ERRORS ==="; $errs | Select-Object -First 6 | ForEach-Object { Write-Host $_.Line } }
                else { Write-Host "=== BUILD OK ===" }
                break
            }
        }
    }
    Write-Host "... waiting ${t}s"
}
