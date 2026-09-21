$newStyle = @"
        <Style x:Key="AltPanelDugme" TargetType="Button">
            <Setter Property="Height" Value="40"/>
            <Setter Property="Padding" Value="16,0"/>
            <Setter Property="Margin" Value="0,0,12,0"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" CornerRadius="8"
                                BorderBrush="#20FFFFFF" BorderThickness="1">
                            <Border.Effect>
                                <DropShadowEffect Color="#000000" BlurRadius="4" ShadowDepth="1" Opacity="0.1"/>
                            </Border.Effect>
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Opacity" Value="0.85"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter Property="Opacity" Value="0.7"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
"@

$urunAltPanel = @"
        <!-- 3. ALT PANEL: İşlem Butonları -->
        <Border Grid.Row="2" Background="White" CornerRadius="12" Padding="16,12" Margin="0,16,0,0">
            <Border.Effect>
                <DropShadowEffect Color="#000000" BlurRadius="10" ShadowDepth="2" Opacity="0.05"/>
            </Border.Effect>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Grid.Column="0" Text="Ürün İşlemleri:" FontSize="14" FontWeight="SemiBold" 
                           Foreground="#4B5563" VerticalAlignment="Center" Margin="0,0,16,0"/>
                
                <StackPanel Grid.Column="1" Orientation="Horizontal">
                    <Button Command="{Binding YeniEkleKomutu}" Style="{StaticResource AltPanelDugme}" Background="#3B82F6">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="➕" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Yeni Ürün" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding DuzenleKomutu}" Style="{StaticResource AltPanelDugme}" Background="#F59E0B">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="✏️" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Düzenle" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding SilKomutu}" Style="{StaticResource AltPanelDugme}" Background="#EF4444">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="🗑️" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Sil" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding StoklarKomutu}" Style="{StaticResource AltPanelDugme}" Background="#10B981">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="📈" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Stok Hareketi" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</UserControl>
"@

$cariAltPanel = @"
        <!-- 3. ALT PANEL: İşlem Butonları -->
        <Border Grid.Row="2" Background="White" CornerRadius="12" Padding="16,12" Margin="0,16,0,0">
            <Border.Effect>
                <DropShadowEffect Color="#000000" BlurRadius="10" ShadowDepth="2" Opacity="0.05"/>
            </Border.Effect>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Grid.Column="0" Text="İşlemler:" FontSize="14" FontWeight="SemiBold" 
                           Foreground="#4B5563" VerticalAlignment="Center" Margin="0,0,16,0"/>
                
                <StackPanel Grid.Column="1" Orientation="Horizontal">
                    <Button Command="{Binding YeniEkleKomutu}" Style="{StaticResource AltPanelDugme}" Background="#3B82F6">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="➕" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Yeni Kayıt" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding DuzenleKomutu}" Style="{StaticResource AltPanelDugme}" Background="#F59E0B">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="✏️" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Düzenle" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding SilKomutu}" Style="{StaticResource AltPanelDugme}" Background="#EF4444">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="🗑️" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Sil" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding CariEkstreAcKomutu}" Style="{StaticResource AltPanelDugme}" Background="#10B981">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="📑" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Hesap Ekstresi" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Command="{Binding TahsilatKomutu}" Style="{StaticResource AltPanelDugme}" Background="#8B5CF6">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="💰" Margin="0,0,8,0" VerticalAlignment="Center"/>
                            <TextBlock Text="Tahsilat / Ödeme" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</UserControl>
"@

function FixFile($path, $altPanel) {
    $content = Get-Content $path -Raw
    
    # Replace RowDefinition height
    $content = $content -replace '<RowDefinition Height="120"\s*/>', '<RowDefinition Height="Auto"/>'
    
    # Replace style block
    $content = $content -replace '(?s)<Style x:Key="AltPanelDugme" TargetType="Button">.*?</Style>', $newStyle
    
    # Replace bottom block
    $content = $content -replace '(?s)<!-- 3\. ALT PANEL: İşlem Butonları -->.*', $altPanel
    
    Set-Content -Path $path -Value $content -Encoding UTF8
}

FixFile "Masaustu\Gorunumler\Sayfa\Musteri\MusteriSayfasi.xaml" $cariAltPanel
FixFile "Masaustu\Gorunumler\Sayfa\Tedarikci\TedarikciSayfasi.xaml" $cariAltPanel
FixFile "Masaustu\Gorunumler\Sayfa\Urun\UrunSayfasi.xaml" $urunAltPanel
