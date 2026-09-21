param([int]$AppProcessId = 36264)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$AppProcessId)
$window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
if (-not $window) { throw 'Uygulama penceresi bulunamadı.' }
$names = @('Dashboard','Cari Listesi','Ürün Listesi','Mağaza & Depo','Siparişler','İrsaliye Yönetimi','Fatura Listesi','Hızlı Satış (POS)','Kasa','Banka','Finans Merkezi','Çek ve Senetler','Rapor Merkezi','Hesap Planı','Muhasebe Fişleri','Yevmiye Defteri','Büyük Defter','Muavin Defter','Mizan','Döviz & Kur Merkezi','Firma İşlemleri','Ayarlar')
$results = @()
foreach ($name in $names) {
    $item = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
    if (-not $item) { $results += [PSCustomObject]@{Screen=$name;State='NOT FOUND'}; continue }
    $button = $item
    while ($button -and $button.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
        $button = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($button)
    }
    if (-not $button) { $results += [PSCustomObject]@{Screen=$name;State='NO BUTTON'}; continue }
    try {
        ([System.Windows.Automation.InvokePattern]$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
        Start-Sleep -Milliseconds 900
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
        $dialogs = @($windows | Where-Object { $_.Current.Name -match 'Hata|Uyarı' })
        $texts = @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text)) | ForEach-Object { $_.Current.Name } | Select-Object -Last 12)
        $results += [PSCustomObject]@{Screen=$name;State=$(if($dialogs.Count){'ERROR DIALOG'}else{'OPENED - CONTENT REVIEW REQUIRED'});Texts=$texts}
        foreach ($dialog in $dialogs) {
            $ok = $dialog.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Tamam'))
            if ($ok) { ([System.Windows.Automation.InvokePattern]$ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke() }
        }
    } catch { $results += [PSCustomObject]@{Screen=$name;State='AUTOMATION ERROR';Error=$_.Exception.GetType().Name} }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'navigation.json') -Encoding UTF8
$results | Select-Object Screen,State | Format-Table -AutoSize
