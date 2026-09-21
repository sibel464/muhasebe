$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class U32 { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n); [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int z,uint f); [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e); }
'@
$ae=[System.Windows.Automation.AutomationElement];$desc=[System.Windows.Automation.TreeScope]::Descendants
function All($r,$t){$c=New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty,$t);@($r.FindAll($desc,$c))}
function Win($n,$sec=12){for($i=0;$i-lt $sec*5;$i++){$c=New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty,$n);$w=$ae::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$c);if($w){return $w};Start-Sleep -Milliseconds 200};throw "Pencere yok: $n"}
function SetV($e,$v){if(!$e){throw 'Edit yok'};($e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($v);Start-Sleep -Milliseconds 250}
function Inv($e){if(!$e){throw 'Button yok'};($e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 700}
function ByName($r,$t,$n){All $r $t|Where-Object{$_.Current.Name-eq$n}|Select-Object -First 1}
function BtnText($r,$n){$t=All $r ([System.Windows.Automation.ControlType]::Text)|Where-Object{$_.Current.Name-eq$n}|Select-Object -First 1;$w=[System.Windows.Automation.TreeWalker]::ControlViewWalker;while($t-and$t.Current.ControlType-ne[System.Windows.Automation.ControlType]::Button){$t=$w.GetParent($t)};$t}
function Send($w,$k){[U32]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle)|Out-Null;$w.SetFocus();[System.Windows.Forms.SendKeys]::SendWait($k);Start-Sleep -Milliseconds 800}
function Dbl($e){$r=$e.Current.BoundingRectangle;[System.Windows.Forms.Cursor]::Position=New-Object Drawing.Point([int]($r.X+$r.Width/2),[int]($r.Y+$r.Height/2));[U32]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[U32]::mouse_event(4,0,0,0,[UIntPtr]::Zero);[U32]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[U32]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 900}
function HasText($r,$p){@((All $r ([System.Windows.Automation.ControlType]::Text))|Where-Object{$_.Current.Name-like$p}).Count-gt0}
function Shot($p,$w,$h,$file){[U32]::ShowWindow([IntPtr]$p.MainWindowHandle,9)|Out-Null;[U32]::SetWindowPos([IntPtr]$p.MainWindowHandle,[IntPtr]::Zero,0,0,$w,$h,4)|Out-Null;Start-Sleep -Seconds 1;$b=New-Object Drawing.Bitmap $w,$h;$g=[Drawing.Graphics]::FromImage($b);$g.CopyFromScreen(0,0,0,0,$b.Size);$b.Save($file,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()}
$p=Start-Process '.\Masaustu\bin\Release\net10.0-windows\Masaustu.exe' -PassThru
try{
 $l=Win 'Muhasebe Yönetim Sistemi - Giriş';$e=All $l ([System.Windows.Automation.ControlType]::Edit);SetV $e[0] 'admin';SetV $e[1] 'admin123';Inv (ByName $l ([System.Windows.Automation.ControlType]::Button) 'Giriş Yap');Start-Sleep -Seconds 2
 $m=$ae::FromHandle($p.MainWindowHandle);Inv (BtnText $m 'Hızlı Satış (POS)');Start-Sleep -Seconds 1;$m=$ae::FromHandle($p.MainWindowHandle)
 Shot $p 1366 768 (Join-Path (Get-Location) 'pos32-1366x768.png');Shot $p 1920 1080 (Join-Path (Get-Location) 'pos32-1920x1080.png')
 $ed=All $m ([System.Windows.Automation.ControlType]::Edit);SetV $ed[1] '10'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Ürün Ara  F2');$u=Win 'Ürün Seçimi';SetV (All $u ([System.Windows.Automation.ControlType]::Edit))[0] 'POS31';$row=All $u ([System.Windows.Automation.ControlType]::DataItem)|Select-Object -First 1;Dbl $row;if(!(HasText $m '*POS31 RUNTIME*')){throw 'F2 çift tık sepete gelmedi'};'F2_DOUBLE=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) '✕');if(HasText $m '*POS31 RUNTIME*'){throw 'F8 silmedi'};'F8=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Ürün Ara  F2');$u=Win 'Ürün Seçimi';SetV (All $u ([System.Windows.Automation.ControlType]::Edit))[0] 'POS31';Send $u '{ENTER}';if(!(HasText $m '*POS31 RUNTIME*')){throw 'F2 Enter sepete gelmedi'};'F2_ENTER=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Cari F3');$c=Win 'Cari Seçimi';SetV (All $c ([System.Windows.Automation.ControlType]::Edit))[0] 'POS31';Send $c '{ENTER}';if(!(HasText $m '*POS31 TEST CARİ*')){throw 'F3 cari gelmedi'};if(!(HasText $m 'Bakiye:*')){throw 'F3 bakiye yok'};'F3=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Düzenle');$s=Win 'Stok Dağılımı Seçimi';foreach($n in 'Mağaza','Depo 1','Depo 2'){if(!(HasText $s $n)){throw "Lokasyon yok $n"}};$se=All $s ([System.Windows.Automation.ControlType]::Edit);SetV $se[0] '2';SetV $se[1] '3';SetV $se[2] '5';Inv (ByName $s ([System.Windows.Automation.ControlType]::Button) 'Uygula');if(!(HasText $m '*Mağaza: 2*Depo 1: 3*Depo 2: 5*')){throw 'F4 özet hatalı'};'F4=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Beklet F5');if(!(HasText $m '*beklemeye alındı*')){throw 'F5 mesaj yok'};'F5=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Bekleyen F6');$b=Win 'Bekleyen Satışlar';if(!(HasText $b '*BEKLE-*')){throw 'F6 listede yok'};Send $b '{ENTER}';if(!(HasText $m '*POS31 RUNTIME*')){throw 'F6 sepet yok'};'F6=PASS'
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Kredi Kartı');if(!(ByName $m ([System.Windows.Automation.ControlType]::ComboBox) '')){'TAKSIT_CONTROL=PASS'};Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'Nakit')
 Inv (ByName $m ([System.Windows.Automation.ControlType]::Button) 'SATIŞI TAMAMLA  F10');Start-Sleep -Seconds 2;if(!(HasText $m '*Satış tamamlandı*')){throw 'F10 mesaj yok'};if(HasText $m '*POS31 RUNTIME*'){throw 'F10 sepet temizlenmedi'};'F10=PASS'
 $focus=$ae::FocusedElement;$mainEd=All $m ([System.Windows.Automation.ControlType]::Edit);if($focus.Current.NativeWindowHandle-ne$mainEd[0].Current.NativeWindowHandle){'FOCUS=FAIL'}else{'FOCUS=PASS'}
 SetV $mainEd[1] '1';SetV $mainEd[0] '8690000031001';Send $m '{ENTER}';Send $m '{ESC}';if(HasText $m '*POS31 RUNTIME*'){throw 'ESC temizlemedi'};'ESC=PASS'
}finally{if(!$p.HasExited){$p.CloseMainWindow()|Out-Null;Start-Sleep -Milliseconds 500;if(!$p.HasExited){$p.Kill()}}}
