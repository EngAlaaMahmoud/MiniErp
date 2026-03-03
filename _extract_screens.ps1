Import-Module ImportExcel
$paths=@(
 'd:\My Files\Pictures\Screenshots\المبيعات.xlsx',
 'd:\My Files\Pictures\Screenshots\المنتج.xlsx',
 'd:\pc\file\فاتوره المشتريات.xlsx',
 'd:\pc\file\شاشه الضرايب.xlsx',
 'd:\pc\file\شاشه تكويد العملاء .xlsx'
)
foreach($p in $paths){
  Write-Host "`n=== $([IO.Path]::GetFileName($p)) ==="
  $pkg = Open-ExcelPackage -Path $p
  foreach($ws in $pkg.Workbook.Worksheets){
    $dim = $ws.Dimension
    $maxRow = if($dim){$dim.End.Row}else{0}
    $maxCol = if($dim){$dim.End.Column}else{0}
    $vals = New-Object System.Collections.Generic.List[object]
    for($r=1;$r -le $maxRow;$r++){
      for($c=1;$c -le $maxCol;$c++){
        $cell=$ws.Cells[$r,$c]
        $v=$cell.Value
        if($null -ne $v -and ("$v").Trim() -ne ''){
          $vals.Add([pscustomobject]@{Addr=$cell.Address;Value=$v})
        }
      }
    }
    $addr = if($dim){$dim.Address}else{""}
    Write-Host "-- $($ws.Name): nonempty=$($vals.Count) dim=$addr"
    $vals | Select-Object -First 60 | ForEach-Object { Write-Host ("   {0}: {1}" -f $_.Addr, $_.Value) }
  }
  Close-ExcelPackage $pkg
}
