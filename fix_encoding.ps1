$files = @(
    "Masaustu\Gorunumler\Sayfa\Urun\UrunSayfasi.xaml",
    "Masaustu\Gorunumler\Sayfa\Musteri\MusteriSayfasi.xaml",
    "Masaustu\Gorunumler\Sayfa\Tedarikci\TedarikciSayfasi.xaml"
)

foreach ($file in $files) {
    # Read the file as UTF-8 (which contains the mangled characters)
    $content = Get-Content $file -Raw -Encoding UTF8
    
    # Convert back to original bytes using Windows-1254 (Turkish ANSI)
    $bytes = [System.Text.Encoding]::GetEncoding(1254).GetBytes($content)
    
    # Decode back to proper string using UTF-8
    $fixedContent = [System.Text.Encoding]::UTF8.GetString($bytes)
    
    # Save back to file as UTF-8
    Set-Content -Path $file -Value $fixedContent -Encoding UTF8
}

Write-Output "Encoding fixed for all 3 files."
