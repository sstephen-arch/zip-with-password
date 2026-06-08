# Rebuild Prompt: Secure Starzip

Build a complete C# WPF desktop application called **Secure Starzip** for Windows 10/11.
Recreate every file exactly as specified below.

---

## Overview

- **App name:** Secure Starzip  
- **Company:** DePaolo Consulting LLC  
- **Version:** 1.1.0  
- **Purpose:** GUI tool to create AES-256 password-protected ZIP archives and extract them. Adds a right-click context menu entry to Windows Explorer.  
- **Framework:** .NET 8, WPF, self-contained single-file exe (win-x64)  
- **NuGet dependency:** DotNetZip 1.16.0 (Ionic.Zip) for AES-256 encryption  
- **CI/CD:** GitHub Actions → produces `SecureStarzip-MSI` artifact via WiX v4.0.5  

---

## Project structure

```
ZipWithPassword/
  ZipWithPassword.csproj
  App.xaml
  App.xaml.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  ZipHelper.cs
  HistoryManager.cs
  SettingsManager.cs
  ContextMenuInstaller.cs
  app.ico               ← generated separately (see icon section)
installer.wxs
gen_installer_assets.py
license.rtf             ← plain RTF, one-liner: "Secure Starzip © 2026 DePaolo Consulting LLC. All rights reserved."
.github/workflows/build.yml
```

---

## File: ZipWithPassword/ZipWithPassword.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>ZipWithPassword</AssemblyName>
    <RootNamespace>ZipWithPassword</RootNamespace>
    <ApplicationIcon>app.ico</ApplicationIcon>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <Platforms>x64</Platforms>
    <Product>Secure Starzip</Product>
    <Company>DePaolo Consulting LLC</Company>
    <Copyright>Copyright © 2026 DePaolo Consulting LLC</Copyright>
    <Version>1.1.0</Version>
    <FileVersion>1.1.0.0</FileVersion>
    <AssemblyVersion>1.1.0.0</AssemblyVersion>
    <Description>Secure file archiving with AES-256 encryption</Description>
  </PropertyGroup>

  <PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DotNetZip" Version="1.16.0" />
  </ItemGroup>

</Project>
```

---

## File: ZipWithPassword/App.xaml

```xml
<Application x:Class="ZipWithPassword.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose">
    <Application.Resources>

        <!-- Dark palette -->
        <SolidColorBrush x:Key="BgPrimaryBrush"     Color="#0D0F17"/>
        <SolidColorBrush x:Key="BgSurfaceBrush"     Color="#13161F"/>
        <SolidColorBrush x:Key="BgCardBrush"        Color="#1B1F2E"/>
        <SolidColorBrush x:Key="AccentBrush"        Color="#6366F1"/>
        <SolidColorBrush x:Key="AccentHoverBrush"   Color="#4F46E5"/>
        <SolidColorBrush x:Key="SuccessBrush"       Color="#22C55E"/>
        <SolidColorBrush x:Key="DangerBrush"        Color="#EF4444"/>
        <SolidColorBrush x:Key="WarningBrush"       Color="#F59E0B"/>
        <SolidColorBrush x:Key="TextPrimaryBrush"   Color="#F1F5F9"/>
        <SolidColorBrush x:Key="TextSecondaryBrush" Color="#64748B"/>
        <SolidColorBrush x:Key="BorderBrush"        Color="#252B3B"/>
        <SolidColorBrush x:Key="NavHoverBrush"      Color="#1E2235"/>
        <SolidColorBrush x:Key="NavActiveBrush"     Color="#252B45"/>

        <!-- TextBox -->
        <Style x:Key="DarkTextBoxStyle" TargetType="TextBox">
            <Setter Property="Background"              Value="Transparent"/>
            <Setter Property="Foreground"              Value="{StaticResource TextPrimaryBrush}"/>
            <Setter Property="BorderThickness"         Value="0"/>
            <Setter Property="Padding"                 Value="12,10"/>
            <Setter Property="FontSize"                Value="13"/>
            <Setter Property="VerticalContentAlignment" Value="Center"/>
            <Setter Property="CaretBrush"              Value="{StaticResource TextPrimaryBrush}"/>
            <Setter Property="SelectionBrush"          Value="{StaticResource AccentBrush}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TextBox">
                        <ScrollViewer x:Name="PART_ContentHost"
                                      Margin="{TemplateBinding Padding}"
                                      VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- PasswordBox -->
        <Style x:Key="DarkPasswordBoxStyle" TargetType="PasswordBox">
            <Setter Property="Background"              Value="Transparent"/>
            <Setter Property="Foreground"              Value="{StaticResource TextPrimaryBrush}"/>
            <Setter Property="BorderThickness"         Value="0"/>
            <Setter Property="Padding"                 Value="12,10"/>
            <Setter Property="FontSize"                Value="13"/>
            <Setter Property="VerticalContentAlignment" Value="Center"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="PasswordBox">
                        <ScrollViewer x:Name="PART_ContentHost"
                                      Margin="{TemplateBinding Padding}"
                                      VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Primary button -->
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background"   Value="{StaticResource AccentBrush}"/>
            <Setter Property="Foreground"   Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding"      Value="24,11"/>
            <Setter Property="FontSize"     Value="13"/>
            <Setter Property="FontWeight"   Value="SemiBold"/>
            <Setter Property="Cursor"       Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bd" Background="{TemplateBinding Background}"
                                CornerRadius="8" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bd" Property="Background" Value="{StaticResource AccentHoverBrush}"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Opacity" Value="0.4"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Ghost button -->
        <Style x:Key="GhostButtonStyle" TargetType="Button">
            <Setter Property="Background"    Value="Transparent"/>
            <Setter Property="Foreground"    Value="{StaticResource TextSecondaryBrush}"/>
            <Setter Property="BorderBrush"   Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding"       Value="18,10"/>
            <Setter Property="FontSize"      Value="13"/>
            <Setter Property="Cursor"        Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bd"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="8" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bd" Property="Background" Value="{StaticResource BgCardBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Nav button -->
        <Style x:Key="NavButtonStyle" TargetType="Button">
            <Setter Property="Background"    Value="Transparent"/>
            <Setter Property="Foreground"    Value="{StaticResource TextSecondaryBrush}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding"       Value="14,11"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="Cursor"        Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bd" Background="{TemplateBinding Background}"
                                CornerRadius="8" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bd" Property="Background" Value="{StaticResource NavHoverBrush}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Input card -->
        <Style x:Key="InputCardStyle" TargetType="Border">
            <Setter Property="Background"      Value="{StaticResource BgCardBrush}"/>
            <Setter Property="BorderBrush"     Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="CornerRadius"    Value="8"/>
        </Style>

        <!-- Scrollbar -->
        <Style TargetType="ScrollBar">
            <Setter Property="Width"       Value="6"/>
            <Setter Property="Background"  Value="Transparent"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid>
                            <Track x:Name="PART_Track" IsDirectionReversed="True">
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border Background="{StaticResource TextSecondaryBrush}" CornerRadius="3"/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

    </Application.Resources>
</Application>
```

---

## File: ZipWithPassword/App.xaml.cs

```csharp
using System.Windows;
using System.Windows.Threading;

namespace ZipWithPassword;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        base.OnStartup(e);

        if (e.Args.Length >= 1)
        {
            var arg = e.Args[0];

            if (arg.Equals("--install", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuInstaller.Install();
                MessageBox.Show(
                    "\"Secure Starzip\" has been added to your right-click context menu.",
                    "Secure Starzip — Installed",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuInstaller.Uninstall();
                MessageBox.Show(
                    "\"Secure Starzip\" has been removed from your right-click context menu.",
                    "Secure Starzip — Uninstalled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }
        }

        string? sourcePath = e.Args.Length >= 1 ? e.Args[0] : null;
        var window = new MainWindow(sourcePath);
        window.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Secure Starzip — Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"A fatal error occurred:\n\n{ex?.Message ?? e.ExceptionObject?.ToString()}",
            "Secure Starzip — Fatal Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

---

## File: ZipWithPassword/MainWindow.xaml

```xml
<Window x:Class="ZipWithPassword.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Secure Starzip"
        Width="860" Height="560"
        MinWidth="780" MinHeight="500"
        WindowStartupLocation="CenterScreen"
        Background="{StaticResource BgPrimaryBrush}"
        Foreground="{StaticResource TextPrimaryBrush}"
        FontFamily="Segoe UI"
        AllowDrop="True">

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </Window.Resources>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="200"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- SIDEBAR -->
        <Border Grid.Column="0" Background="{StaticResource BgSurfaceBrush}"
                BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,0,1,0">
            <DockPanel>
                <StackPanel DockPanel.Dock="Top" Margin="20,24,20,28">
                    <StackPanel Orientation="Horizontal">
                        <Border Width="32" Height="32" Background="{StaticResource AccentBrush}"
                                CornerRadius="8" Margin="0,0,10,0">
                            <TextBlock Text="*" FontSize="17" Foreground="White"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <StackPanel VerticalAlignment="Center">
                            <TextBlock Text="Secure" FontSize="14" FontWeight="Bold"
                                       Foreground="{StaticResource TextPrimaryBrush}"/>
                            <TextBlock Text="Starzip" FontSize="14" FontWeight="Bold"
                                       Foreground="{StaticResource AccentBrush}" Margin="0,-3,0,0"/>
                        </StackPanel>
                    </StackPanel>
                    <TextBlock Text="AES-256 protected archives" FontSize="10"
                               Foreground="{StaticResource TextSecondaryBrush}" Margin="42,-2,0,0"/>
                </StackPanel>

                <StackPanel DockPanel.Dock="Top" Margin="12,0">
                    <Button x:Name="NavZip"     Click="Nav_Click" Tag="Zip"
                            Style="{StaticResource NavButtonStyle}" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="[ZIP]" FontSize="14" Margin="0,0,10,0"/>
                            <TextBlock Text="Zip / Encrypt" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button x:Name="NavUnzip"   Click="Nav_Click" Tag="Unzip"
                            Style="{StaticResource NavButtonStyle}" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="[OPEN]" FontSize="14" Margin="0,0,10,0"/>
                            <TextBlock Text="Unzip / Decrypt" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button x:Name="NavHistory" Click="Nav_Click" Tag="History"
                            Style="{StaticResource NavButtonStyle}" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="[H]" FontSize="14" Margin="0,0,10,0"/>
                            <TextBlock Text="History" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button x:Name="NavSettings" Click="Nav_Click" Tag="Settings"
                            Style="{StaticResource NavButtonStyle}" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="" FontSize="14" Margin="0,0,10,0"/>
                            <TextBlock Text="Settings" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>

                <TextBlock DockPanel.Dock="Bottom" Text="v1.1.0" FontSize="10"
                           Foreground="{StaticResource TextSecondaryBrush}"
                           Margin="20,0,20,16" HorizontalAlignment="Left"/>
            </DockPanel>
        </Border>

        <!-- CONTENT AREA -->
        <Grid Grid.Column="1">

            <!-- ZIP SECTION -->
            <ScrollViewer x:Name="ZipSection" VerticalScrollBarVisibility="Auto"
                          Background="Transparent">
                <StackPanel Margin="32,28,32,28">
                    <TextBlock Text="Zip &amp; Encrypt" FontSize="20" FontWeight="Bold"
                               Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                    <TextBlock Text="Add files or folders, set a password, and create an AES-256 encrypted archive."
                               FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,0,24"
                               TextWrapping="Wrap"/>

                    <!-- Drop zone -->
                    <Border x:Name="DropZone" Height="130" CornerRadius="12"
                            BorderBrush="{StaticResource BorderBrush}" BorderThickness="2"
                            Background="{StaticResource BgCardBrush}" Margin="0,0,0,20"
                            AllowDrop="True" Drop="DropZone_Drop" DragOver="DropZone_DragOver"
                            DragLeave="DropZone_DragLeave" DragEnter="DropZone_DragEnter">
                        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                            <TextBlock Text="^" FontSize="28" Foreground="{StaticResource AccentBrush}"
                                       HorizontalAlignment="Center" Margin="0,0,0,8"/>
                            <TextBlock Text="Drag &amp; drop files or folders here"
                                       FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Center"/>
                            <TextBlock Text="or" FontSize="11"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Center" Margin="0,4"/>
                            <Button Content="Browse files..." Style="{StaticResource GhostButtonStyle}"
                                    Padding="16,7" FontSize="12" HorizontalAlignment="Center"
                                    Click="ZipBrowseSource_Click"/>
                        </StackPanel>
                    </Border>

                    <TextBlock Text="SOURCE" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Border Style="{StaticResource InputCardStyle}" Margin="0,0,0,16">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="ZipSourceBox" Style="{StaticResource DarkTextBoxStyle}"
                                     IsReadOnly="True" Foreground="{StaticResource TextSecondaryBrush}"
                                     Text="No file or folder selected"/>
                            <Button Grid.Column="1" Content="Change" Style="{StaticResource GhostButtonStyle}"
                                    Padding="12,8" FontSize="11" Margin="0,4,4,4"
                                    Click="ZipBrowseSource_Click"/>
                        </Grid>
                    </Border>

                    <TextBlock Text="SAVE AS" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Border Style="{StaticResource InputCardStyle}" Margin="0,0,0,16">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="ZipOutputBox" Style="{StaticResource DarkTextBoxStyle}"/>
                            <Button Grid.Column="1" Content="Browse" Style="{StaticResource GhostButtonStyle}"
                                    Padding="12,8" FontSize="11" Margin="0,4,4,4"
                                    Click="ZipBrowseOutput_Click"/>
                        </Grid>
                    </Border>

                    <Grid Margin="0,0,0,16">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="12"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0">
                            <TextBlock Text="PASSWORD" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                                       Foreground="{StaticResource TextSecondaryBrush}"/>
                            <Border Style="{StaticResource InputCardStyle}">
                                <PasswordBox x:Name="ZipPasswordBox" Style="{StaticResource DarkPasswordBoxStyle}"
                                             PasswordChanged="ZipPassword_Changed"/>
                            </Border>
                        </StackPanel>
                        <StackPanel Grid.Column="2">
                            <TextBlock Text="CONFIRM PASSWORD" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                                       Foreground="{StaticResource TextSecondaryBrush}"/>
                            <Border Style="{StaticResource InputCardStyle}">
                                <PasswordBox x:Name="ZipConfirmBox" Style="{StaticResource DarkPasswordBoxStyle}"
                                             PasswordChanged="ZipPassword_Changed"/>
                            </Border>
                        </StackPanel>
                    </Grid>

                    <Grid Margin="0,0,0,20">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0" VerticalAlignment="Center">
                            <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                                <TextBlock x:Name="StrengthLabel" Text="Strength: --"
                                           FontSize="11" Foreground="{StaticResource TextSecondaryBrush}"/>
                            </StackPanel>
                            <Grid Height="4">
                                <Border Background="{StaticResource BgCardBrush}" CornerRadius="2"/>
                                <Border x:Name="StrengthBar" CornerRadius="2"
                                        HorizontalAlignment="Left" Width="0"
                                        Background="{StaticResource AccentBrush}"/>
                            </Grid>
                        </StackPanel>
                        <Button Grid.Column="1" Content="* Generate password"
                                Style="{StaticResource GhostButtonStyle}"
                                Padding="14,8" FontSize="12" Margin="12,0,0,0"
                                Click="GeneratePassword_Click"/>
                    </Grid>

                    <TextBlock x:Name="ZipErrorText" FontSize="12" Margin="0,0,0,12"
                               Foreground="{StaticResource DangerBrush}" TextWrapping="Wrap"
                               Visibility="Collapsed"/>

                    <StackPanel x:Name="ZipProgressPanel" Visibility="Collapsed" Margin="0,0,0,16">
                        <Grid Margin="0,0,0,6">
                            <TextBlock x:Name="ZipStatusText" FontSize="12"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Left" Text="Working..."/>
                            <TextBlock x:Name="ZipPercentText" FontSize="12"
                                       Foreground="{StaticResource AccentBrush}"
                                       HorizontalAlignment="Right"/>
                        </Grid>
                        <Border Height="6" CornerRadius="3" Background="{StaticResource BgCardBrush}">
                            <Border x:Name="ZipProgressBar" CornerRadius="3"
                                    Background="{StaticResource AccentBrush}"
                                    HorizontalAlignment="Left" Width="0"/>
                        </Border>
                    </StackPanel>

                    <Border x:Name="ZipSuccessBanner" CornerRadius="8" Padding="14,10"
                            Background="#0F2A1A" BorderBrush="{StaticResource SuccessBrush}"
                            BorderThickness="1" Margin="0,0,0,16" Visibility="Collapsed">
                        <TextBlock Foreground="{StaticResource SuccessBrush}" FontSize="13" FontWeight="SemiBold"
                                   Text=" Archive created successfully!"/>
                    </Border>

                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                        <Button x:Name="ZipCreateButton" Content="[ZIP]  Create ZIP"
                                Style="{StaticResource PrimaryButtonStyle}"
                                Padding="28,12" FontSize="14" Click="ZipCreate_Click"/>
                    </StackPanel>
                </StackPanel>
            </ScrollViewer>

            <!-- UNZIP SECTION -->
            <ScrollViewer x:Name="UnzipSection" VerticalScrollBarVisibility="Auto"
                          Background="Transparent" Visibility="Collapsed">
                <StackPanel Margin="32,28,32,28">
                    <TextBlock Text="Unzip &amp; Decrypt" FontSize="20" FontWeight="Bold"
                               Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                    <TextBlock Text="Select an encrypted ZIP archive and enter its password to extract the contents."
                               FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,0,24"
                               TextWrapping="Wrap"/>

                    <Border x:Name="UnzipDropZone" Height="120" CornerRadius="12"
                            BorderBrush="{StaticResource BorderBrush}" BorderThickness="2"
                            Background="{StaticResource BgCardBrush}" Margin="0,0,0,20"
                            AllowDrop="True" Drop="UnzipDropZone_Drop" DragOver="DropZone_DragOver"
                            DragLeave="UnzipDropZone_DragLeave" DragEnter="UnzipDropZone_DragEnter">
                        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                            <TextBlock Text="[PKG]" FontSize="28" HorizontalAlignment="Center" Margin="0,0,0,8"/>
                            <TextBlock Text="Drop a ZIP file here"
                                       FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Center"/>
                            <TextBlock Text="or" FontSize="11"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Center" Margin="0,4"/>
                            <Button Content="Browse ZIP..." Style="{StaticResource GhostButtonStyle}"
                                    Padding="16,7" FontSize="12" HorizontalAlignment="Center"
                                    Click="UnzipBrowseSource_Click"/>
                        </StackPanel>
                    </Border>

                    <TextBlock Text="ZIP FILE" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Border Style="{StaticResource InputCardStyle}" Margin="0,0,0,16">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="UnzipSourceBox" Style="{StaticResource DarkTextBoxStyle}"
                                     IsReadOnly="True" Foreground="{StaticResource TextSecondaryBrush}"
                                     Text="No ZIP file selected"/>
                            <Button Grid.Column="1" Content="Change" Style="{StaticResource GhostButtonStyle}"
                                    Padding="12,8" FontSize="11" Margin="0,4,4,4"
                                    Click="UnzipBrowseSource_Click"/>
                        </Grid>
                    </Border>

                    <TextBlock Text="EXTRACT TO" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Border Style="{StaticResource InputCardStyle}" Margin="0,0,0,16">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="UnzipOutputBox" Style="{StaticResource DarkTextBoxStyle}"/>
                            <Button Grid.Column="1" Content="Browse" Style="{StaticResource GhostButtonStyle}"
                                    Padding="12,8" FontSize="11" Margin="0,4,4,4"
                                    Click="UnzipBrowseOutput_Click"/>
                        </Grid>
                    </Border>

                    <TextBlock Text="PASSWORD" FontSize="10" FontWeight="SemiBold" Margin="0,0,0,5"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Border Style="{StaticResource InputCardStyle}" Margin="0,0,0,20">
                        <PasswordBox x:Name="UnzipPasswordBox" Style="{StaticResource DarkPasswordBoxStyle}"/>
                    </Border>

                    <TextBlock x:Name="UnzipErrorText" FontSize="12" Margin="0,0,0,12"
                               Foreground="{StaticResource DangerBrush}" TextWrapping="Wrap"
                               Visibility="Collapsed"/>

                    <StackPanel x:Name="UnzipProgressPanel" Visibility="Collapsed" Margin="0,0,0,16">
                        <Grid Margin="0,0,0,6">
                            <TextBlock x:Name="UnzipStatusText" FontSize="12"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       HorizontalAlignment="Left" Text="Extracting..."/>
                            <TextBlock x:Name="UnzipPercentText" FontSize="12"
                                       Foreground="{StaticResource AccentBrush}"
                                       HorizontalAlignment="Right"/>
                        </Grid>
                        <Border Height="6" CornerRadius="3" Background="{StaticResource BgCardBrush}">
                            <Border x:Name="UnzipProgressBar" CornerRadius="3"
                                    Background="{StaticResource AccentBrush}"
                                    HorizontalAlignment="Left" Width="0"/>
                        </Border>
                    </StackPanel>

                    <Border x:Name="UnzipSuccessBanner" CornerRadius="8" Padding="14,10"
                            Background="#0F2A1A" BorderBrush="{StaticResource SuccessBrush}"
                            BorderThickness="1" Margin="0,0,0,16" Visibility="Collapsed">
                        <TextBlock Foreground="{StaticResource SuccessBrush}" FontSize="13" FontWeight="SemiBold"
                                   Text=" Files extracted successfully!"/>
                    </Border>

                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                        <Button x:Name="UnzipButton" Content="[OPEN]  Extract files"
                                Style="{StaticResource PrimaryButtonStyle}"
                                Padding="28,12" FontSize="14" Click="Unzip_Click"/>
                    </StackPanel>
                </StackPanel>
            </ScrollViewer>

            <!-- HISTORY SECTION -->
            <Grid x:Name="HistorySection" Visibility="Collapsed">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <StackPanel Grid.Row="0" Margin="32,28,32,16">
                    <Grid>
                        <StackPanel>
                            <TextBlock Text="History" FontSize="20" FontWeight="Bold"
                                       Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                            <TextBlock Text="Recent zip and unzip operations."
                                       FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"/>
                        </StackPanel>
                        <Button Content="Clear all" Style="{StaticResource GhostButtonStyle}"
                                Padding="12,7" FontSize="12" HorizontalAlignment="Right"
                                VerticalAlignment="Top" Click="HistoryClear_Click"/>
                    </Grid>
                </StackPanel>
                <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Margin="32,0,32,0">
                    <ItemsControl x:Name="HistoryList">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border CornerRadius="8" Background="{StaticResource BgCardBrush}"
                                        BorderBrush="{StaticResource BorderBrush}" BorderThickness="1"
                                        Padding="16,12" Margin="0,0,0,8">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Width="36" Height="36"
                                                CornerRadius="8" Margin="0,0,12,0"
                                                Background="{StaticResource BgSurfaceBrush}">
                                            <TextBlock Text="{Binding TypeIcon}"
                                                       FontSize="16" HorizontalAlignment="Center"
                                                       VerticalAlignment="Center"/>
                                        </Border>
                                        <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                            <TextBlock Text="{Binding OutputPath}"
                                                       FontSize="13" FontWeight="SemiBold"
                                                       Foreground="{StaticResource TextPrimaryBrush}"
                                                       TextTrimming="CharacterEllipsis"/>
                                            <TextBlock Text="{Binding SourcePath}"
                                                       FontSize="11"
                                                       Foreground="{StaticResource TextSecondaryBrush}"
                                                       TextTrimming="CharacterEllipsis"/>
                                        </StackPanel>
                                        <TextBlock Grid.Column="2"
                                                   Text="{Binding FormattedTime}"
                                                   FontSize="11"
                                                   Foreground="{StaticResource TextSecondaryBrush}"
                                                   VerticalAlignment="Center" Margin="12,0,0,0"/>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
                <TextBlock x:Name="HistoryEmpty" Grid.Row="1"
                           Text="No operations yet. Zip or unzip a file to get started."
                           FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           Visibility="Collapsed"/>
                <TextBlock Grid.Row="2" Margin="32,8,32,20"
                           FontSize="11" Foreground="{StaticResource TextSecondaryBrush}"
                           x:Name="HistoryCount"/>
            </Grid>

            <!-- SETTINGS SECTION -->
            <ScrollViewer x:Name="SettingsSection" Visibility="Collapsed"
                          VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="32,28,32,28">
                    <TextBlock Text="Settings" FontSize="20" FontWeight="Bold"
                               Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                    <TextBlock Text="Configure Secure Starzip to fit your workflow."
                               FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,0,28"
                               TextWrapping="Wrap"/>

                    <Border CornerRadius="10" Background="{StaticResource BgCardBrush}"
                            BorderBrush="{StaticResource BorderBrush}" BorderThickness="1"
                            Padding="20" Margin="0,0,0,16">
                        <StackPanel>
                            <TextBlock Text="Default output folder" FontSize="14" FontWeight="SemiBold"
                                       Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                            <TextBlock Text="Where ZIP files are saved by default when you don't specify a path."
                                       FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"
                                       Margin="0,0,0,12" TextWrapping="Wrap"/>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <Border Style="{StaticResource InputCardStyle}">
                                    <TextBox x:Name="DefaultFolderBox" Style="{StaticResource DarkTextBoxStyle}"/>
                                </Border>
                                <Button Grid.Column="1" Content="Browse" Style="{StaticResource GhostButtonStyle}"
                                        Padding="14,10" FontSize="12" Margin="8,0,0,0"
                                        Click="SettingsBrowseFolder_Click"/>
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border CornerRadius="10" Background="{StaticResource BgCardBrush}"
                            BorderBrush="{StaticResource BorderBrush}" BorderThickness="1"
                            Padding="20" Margin="0,0,0,16">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0" Margin="0,0,20,0">
                                <TextBlock Text="Right-click context menu" FontSize="14" FontWeight="SemiBold"
                                           Foreground="{StaticResource TextPrimaryBrush}" Margin="0,0,0,4"/>
                                <TextBlock x:Name="CtxMenuStatusText"
                                           FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"
                                           TextWrapping="Wrap"/>
                            </StackPanel>
                            <Button x:Name="CtxMenuButton" Grid.Column="1"
                                    Style="{StaticResource GhostButtonStyle}"
                                    Padding="16,10" FontSize="12" VerticalAlignment="Center"
                                    Click="CtxMenu_Click"/>
                        </Grid>
                    </Border>

                    <Button Content="Save settings" Style="{StaticResource PrimaryButtonStyle}"
                            Padding="24,12" FontSize="13" HorizontalAlignment="Left"
                            Click="SaveSettings_Click"/>

                    <Border x:Name="SettingsSavedBanner" CornerRadius="8" Padding="14,10"
                            Background="#0F2A1A" BorderBrush="{StaticResource SuccessBrush}"
                            BorderThickness="1" Margin="0,12,0,0" Visibility="Collapsed">
                        <TextBlock Foreground="{StaticResource SuccessBrush}" FontSize="13" FontWeight="SemiBold"
                                   Text=" Settings saved."/>
                    </Border>
                </StackPanel>
            </ScrollViewer>

        </Grid>
    </Grid>
</Window>
```

---

## File: ZipWithPassword/MainWindow.xaml.cs

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZipWithPassword;

public partial class MainWindow : Window
{
    private string _activeSection = "Zip";
    private AppSettings _settings;

    public MainWindow(string? sourcePath)
    {
        InitializeComponent();
        _settings = SettingsManager.Load();

        if (!string.IsNullOrWhiteSpace(sourcePath) && sourcePath != "--install" && sourcePath != "--uninstall")
            SetZipSource(sourcePath);

        ShowSection("Zip");
        RefreshHistorySection();
        RefreshSettingsSection();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ShowSection(tag);
    }

    private void ShowSection(string section)
    {
        _activeSection = section;
        ZipSection.Visibility     = section == "Zip"      ? Visibility.Visible : Visibility.Collapsed;
        UnzipSection.Visibility   = section == "Unzip"    ? Visibility.Visible : Visibility.Collapsed;
        HistorySection.Visibility = section == "History"  ? Visibility.Visible : Visibility.Collapsed;
        SettingsSection.Visibility= section == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        foreach (var btn in new[] { NavZip, NavUnzip, NavHistory, NavSettings })
        {
            bool active = btn.Tag?.ToString() == section;
            btn.Background = active
                ? (SolidColorBrush)FindResource("NavActiveBrush")
                : Brushes.Transparent;
            btn.Foreground = active
                ? (SolidColorBrush)FindResource("TextPrimaryBrush")
                : (SolidColorBrush)FindResource("TextSecondaryBrush");
        }

        if (section == "History")  RefreshHistorySection();
        if (section == "Settings") RefreshSettingsSection();
    }

    private void SetZipSource(string path)
    {
        ZipSourceBox.Text = path;
        ZipOutputBox.Text = BuildDefaultZipOutput(path);
    }

    private static string BuildDefaultZipOutput(string source)
    {
        source = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string dir  = Path.GetDirectoryName(source)
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string name = Path.GetFileNameWithoutExtension(source);
        if (string.IsNullOrWhiteSpace(name))
            name = new DirectoryInfo(source).Name;
        string candidate = Path.Combine(dir, name + ".zip");
        int i = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{name} ({i++}).zip");
        return candidate;
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)  => HighlightDropZone(DropZone, true);
    private void DropZone_DragLeave(object sender, DragEventArgs e)  => HighlightDropZone(DropZone, false);
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        HighlightDropZone(DropZone, false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetZipSource(files[0]);
    }

    private void ZipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a file to zip",
            Filter = "All Files (*.*)|*.*",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Select Folder or File"
        };
        if (dlg.ShowDialog() == true)
        {
            string selected = dlg.FileName;
            if (Directory.Exists(selected))       SetZipSource(selected);
            else if (File.Exists(selected))       SetZipSource(selected);
            else
            {
                string folder = BrowseForFolder("Select folder to zip");
                if (!string.IsNullOrEmpty(folder)) SetZipSource(folder);
            }
        }
    }

    private void ZipBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save ZIP as...",
            Filter = "ZIP Archive (*.zip)|*.zip",
            DefaultExt = ".zip",
            FileName = Path.GetFileName(ZipOutputBox.Text),
            InitialDirectory = Path.GetDirectoryName(ZipOutputBox.Text)
                            ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() == true) ZipOutputBox.Text = dlg.FileName;
    }

    private void ZipPassword_Changed(object sender, RoutedEventArgs e)
    {
        ZipErrorText.Visibility = Visibility.Collapsed;
        UpdateStrengthBar();
    }

    private void UpdateStrengthBar()
    {
        string pwd = ZipPasswordBox.Password;
        int score  = ScorePassword(pwd);
        double maxWidth = ((Border)StrengthBar.Parent).ActualWidth;
        if (maxWidth <= 0) maxWidth = 200;
        StrengthBar.Width = maxWidth * score / 4.0;
        StrengthBar.Background = score switch
        {
            0 => (SolidColorBrush)FindResource("TextSecondaryBrush"),
            1 => (SolidColorBrush)FindResource("DangerBrush"),
            2 => (SolidColorBrush)FindResource("WarningBrush"),
            3 => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
            _ => (SolidColorBrush)FindResource("SuccessBrush"),
        };
        StrengthLabel.Text = "Strength: " + score switch
        {
            0 => "--", 1 => "Weak", 2 => "Fair", 3 => "Good", _ => "Strong",
        };
    }

    private static int ScorePassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return 0;
        int score = 0;
        if (pwd.Length >= 8)  score++;
        if (pwd.Length >= 14) score++;
        if (pwd.Any(char.IsUpper) && pwd.Any(char.IsLower)) score++;
        if (pwd.Any(char.IsDigit) && pwd.Any(c => !char.IsLetterOrDigit(c))) score++;
        return score;
    }

    private void GeneratePassword_Click(object sender, RoutedEventArgs e)
    {
        string pwd = GenerateStrongPassword(20);
        ZipPasswordBox.Password = pwd;
        ZipConfirmBox.Password  = pwd;
        UpdateStrengthBar();
        var result = MessageBox.Show(
            $"Generated password:\n\n{pwd}\n\nCopy this somewhere safe.\n\nClick OK to use it, or Cancel to enter your own.",
            "Secure Starzip — Generated Password",
            MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (result != MessageBoxResult.OK)
        {
            ZipPasswordBox.Password = string.Empty;
            ZipConfirmBox.Password  = string.Empty;
            UpdateStrengthBar();
        }
    }

    public static string GenerateStrongPassword(int length = 20)
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "!@#$%^&*-_=+?";
        string pool = upper + lower + digits + special;
        var bytes = new byte[length * 2];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(length);
        sb.Append(upper[bytes[0]   % upper.Length]);
        sb.Append(lower[bytes[1]   % lower.Length]);
        sb.Append(digits[bytes[2]  % digits.Length]);
        sb.Append(special[bytes[3] % special.Length]);
        for (int i = 4; i < length; i++)
            sb.Append(pool[(bytes[i] + bytes[i + length]) % pool.Length]);
        var arr = sb.ToString().ToCharArray();
        var shuffleBytes = new byte[arr.Length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return new string(arr);
    }

    private bool ValidateZip(out string error)
    {
        if (string.IsNullOrWhiteSpace(ZipSourceBox.Text) || ZipSourceBox.Text == "No file or folder selected")
        { error = "Please select a file or folder to zip."; return false; }
        if (string.IsNullOrWhiteSpace(ZipOutputBox.Text))
        { error = "Please specify a destination path."; return false; }
        if (!ZipOutputBox.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        { error = "Destination must end with .zip"; return false; }
        if (ZipPasswordBox.Password.Length == 0)
        { error = "Please enter a password."; return false; }
        if (ZipPasswordBox.Password.Length < 6)
        { error = "Password must be at least 6 characters."; return false; }
        if (ZipPasswordBox.Password != ZipConfirmBox.Password)
        { error = "Passwords do not match."; return false; }
        error = string.Empty;
        return true;
    }

    private async void ZipCreate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateZip(out string error)) { ShowZipError(error); return; }
        ZipErrorText.Visibility    = Visibility.Collapsed;
        ZipSuccessBanner.Visibility = Visibility.Collapsed;
        ZipProgressPanel.Visibility = Visibility.Visible;
        ZipCreateButton.IsEnabled   = false;

        string source   = ZipSourceBox.Text.TrimEnd('\\', '/');
        string output   = ZipOutputBox.Text;
        string password = ZipPasswordBox.Password;
        var progressWidth = ((Border)ZipProgressBar.Parent).ActualWidth;
        var prog = new Progress<(int pct, string status)>(p =>
        {
            ZipProgressBar.Width = progressWidth * p.pct / 100.0;
            ZipPercentText.Text  = $"{p.pct}%";
            ZipStatusText.Text   = p.status;
        });

        bool success = false; string? errMsg = null;
        try { await Task.Run(() => ZipHelper.CreateEncryptedZip(source, output, password, prog)); success = true; }
        catch (Exception ex) { errMsg = ex.Message; }

        ZipProgressPanel.Visibility = Visibility.Collapsed;
        ZipCreateButton.IsEnabled   = true;
        HistoryManager.Add(new HistoryEntry { Type = OperationType.Zip, SourcePath = source, OutputPath = output, Success = success, ErrorMessage = errMsg ?? string.Empty });

        if (success) ZipSuccessBanner.Visibility = Visibility.Visible;
        else ShowZipError($"Error: {errMsg}");
    }

    private void ShowZipError(string msg) { ZipErrorText.Text = msg; ZipErrorText.Visibility = Visibility.Visible; }

    private void UnzipDropZone_DragEnter(object sender, DragEventArgs e) => HighlightDropZone(UnzipDropZone, true);
    private void UnzipDropZone_DragLeave(object sender, DragEventArgs e) => HighlightDropZone(UnzipDropZone, false);
    private void UnzipDropZone_Drop(object sender, DragEventArgs e)
    {
        HighlightDropZone(UnzipDropZone, false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetUnzipSource(files[0]);
    }

    private void SetUnzipSource(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        { ShowUnzipError("Please select a .zip file."); return; }
        UnzipSourceBox.Text = path;
        UnzipOutputBox.Text = Path.Combine(
            Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.GetFileNameWithoutExtension(path));
    }

    private void UnzipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Select ZIP file to extract", Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*" };
        if (dlg.ShowDialog() == true) SetUnzipSource(dlg.FileName);
    }

    private void UnzipBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        string folder = BrowseForFolder("Select folder to extract into");
        if (!string.IsNullOrEmpty(folder)) UnzipOutputBox.Text = folder;
    }

    private async void Unzip_Click(object sender, RoutedEventArgs e)
    {
        UnzipErrorText.Visibility    = Visibility.Collapsed;
        UnzipSuccessBanner.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(UnzipSourceBox.Text) || UnzipSourceBox.Text == "No ZIP file selected")
        { ShowUnzipError("Please select a ZIP file."); return; }
        if (string.IsNullOrWhiteSpace(UnzipOutputBox.Text))
        { ShowUnzipError("Please specify an output folder."); return; }
        if (UnzipPasswordBox.Password.Length == 0)
        { ShowUnzipError("Please enter the ZIP password."); return; }

        UnzipProgressPanel.Visibility = Visibility.Visible;
        UnzipButton.IsEnabled          = false;

        string zipPath   = UnzipSourceBox.Text;
        string outFolder = UnzipOutputBox.Text;
        string password  = UnzipPasswordBox.Password;
        double progWidth = ((Border)UnzipProgressBar.Parent).ActualWidth;
        var prog = new Progress<(int pct, string status)>(p =>
        {
            UnzipProgressBar.Width = progWidth * p.pct / 100.0;
            UnzipPercentText.Text  = $"{p.pct}%";
            UnzipStatusText.Text   = p.status;
        });

        bool success = false; string? errMsg = null;
        try { await Task.Run(() => ZipHelper.ExtractEncryptedZip(zipPath, outFolder, password, prog)); success = true; }
        catch (Exception ex) { errMsg = ex.Message; }

        UnzipProgressPanel.Visibility = Visibility.Collapsed;
        UnzipButton.IsEnabled          = true;
        HistoryManager.Add(new HistoryEntry { Type = OperationType.Unzip, SourcePath = zipPath, OutputPath = outFolder, Success = success, ErrorMessage = errMsg ?? string.Empty });

        if (success) UnzipSuccessBanner.Visibility = Visibility.Visible;
        else ShowUnzipError(errMsg?.Contains("password", StringComparison.OrdinalIgnoreCase) == true
            ? "Wrong password or file is not encrypted." : $"Error: {errMsg}");
    }

    private void ShowUnzipError(string msg) { UnzipErrorText.Text = msg; UnzipErrorText.Visibility = Visibility.Visible; }

    private void RefreshHistorySection()
    {
        var entries = HistoryManager.Load();
        var vms = entries.Select(h => new HistoryViewModel(h)).ToList();
        HistoryList.ItemsSource = vms;
        HistoryEmpty.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryCount.Text       = vms.Count == 0 ? string.Empty : $"{vms.Count} operation(s)";
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all history?", "Secure Starzip", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        { HistoryManager.Clear(); RefreshHistorySection(); }
    }

    private void RefreshSettingsSection()
    {
        DefaultFolderBox.Text = _settings.DefaultOutputFolder;
        bool installed = ContextMenuInstaller.IsInstalled();
        CtxMenuStatusText.Text = installed
            ? "Currently installed. \"Secure Starzip -- Zip with password...\" appears when you right-click files and folders."
            : "Not installed. Click the button to add Secure Starzip to your right-click menu (requires admin).";
        CtxMenuButton.Content = installed ? "Uninstall" : "Install";
    }

    private void SettingsBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        string folder = BrowseForFolder("Select default output folder");
        if (!string.IsNullOrEmpty(folder)) DefaultFolderBox.Text = folder;
    }

    private void CtxMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ContextMenuInstaller.IsInstalled()) ContextMenuInstaller.Uninstall();
            else ContextMenuInstaller.Install();
            RefreshSettingsSection();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update context menu:\n{ex.Message}\n\nTry running Secure Starzip as Administrator.",
                "Secure Starzip", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultOutputFolder = DefaultFolderBox.Text;
        SettingsManager.Save(_settings);
        SettingsSavedBanner.Visibility = Visibility.Visible;
    }

    private static void HighlightDropZone(Border zone, bool on)
    {
        zone.BorderBrush = on
            ? new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1))
            : new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x3B));
        zone.Background = on
            ? new SolidColorBrush(Color.FromArgb(0x18, 0x63, 0x66, 0xF1))
            : new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x2E));
    }

    private static string BrowseForFolder(string description)
    {
        var dlg = new OpenFileDialog
        {
            Title = description, CheckFileExists = false, CheckPathExists = true,
            FileName = "Select Folder", Filter = "Folders|*.none", ValidateNames = false
        };
        if (dlg.ShowDialog() == true) return Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        return string.Empty;
    }
}

public class HistoryViewModel
{
    private readonly HistoryEntry _entry;
    public HistoryViewModel(HistoryEntry entry) => _entry = entry;
    public string TypeIcon     => _entry.Type == OperationType.Zip ? "[ZIP]" : "[OPEN]";
    public string SourcePath   => _entry.SourcePath;
    public string OutputPath   => _entry.OutputPath;
    public string FormattedTime => _entry.Timestamp.ToString("MMM d, h:mm tt");
}
```

---

## File: ZipWithPassword/ZipHelper.cs

```csharp
using System.IO;
using Ionic.Zip;

namespace ZipWithPassword;

public static class ZipHelper
{
    public static void CreateEncryptedZip(string sourcePath, string outputPath, string password,
        IProgress<(int percent, string status)>? progress = null)
    {
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        using var zip = new ZipFile();
        zip.Password         = password;
        zip.Encryption       = EncryptionAlgorithm.WinZipAes256;
        zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;

        zip.SaveProgress += (_, args) =>
        {
            if (args.EventType == ZipProgressEventType.Saving_BeforeWriteEntry && args.EntriesTotal > 0)
                progress?.Report(((int)((double)args.EntriesSaved / args.EntriesTotal * 100),
                    $"Compressing {Path.GetFileName(args.CurrentEntry.FileName)}..."));
            else if (args.EventType == ZipProgressEventType.Saving_Completed)
                progress?.Report((100, "Done"));
        };

        if (Directory.Exists(sourcePath))
            zip.AddDirectory(sourcePath, new DirectoryInfo(sourcePath).Name);
        else
            zip.AddFile(sourcePath, string.Empty);

        zip.Save(outputPath);
    }

    public static void ExtractEncryptedZip(string zipPath, string outputFolder, string password,
        IProgress<(int percent, string status)>? progress = null)
    {
        Directory.CreateDirectory(outputFolder);
        using var zip = ZipFile.Read(zipPath);
        zip.Password = password;
        int total = zip.Count, done = 0;

        zip.ExtractProgress += (_, args) =>
        {
            if (args.EventType == ZipProgressEventType.Extracting_AfterExtractEntry)
            {
                done++;
                progress?.Report((total > 0 ? (int)((double)done / total * 100) : 0,
                    $"Extracting {Path.GetFileName(args.CurrentEntry.FileName)}..."));
            }
            else if (args.EventType == ZipProgressEventType.Extracting_AfterExtractAll)
                progress?.Report((100, "Done"));
        };

        foreach (var entry in zip)
            entry.Extract(outputFolder, ExtractExistingFileAction.OverwriteSilently);
    }
}
```

---

## File: ZipWithPassword/HistoryManager.cs

```csharp
using System.IO;
using System.Text.Json;

namespace ZipWithPassword;

public enum OperationType { Zip, Unzip }

public class HistoryEntry
{
    public OperationType Type         { get; set; }
    public string        SourcePath   { get; set; } = string.Empty;
    public string        OutputPath   { get; set; } = string.Empty;
    public DateTime      Timestamp    { get; set; } = DateTime.Now;
    public bool          Success      { get; set; } = true;
    public string        ErrorMessage { get; set; } = string.Empty;
}

public static class HistoryManager
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SecureStarzip", "history.json");

    public static List<HistoryEntry> Load()
    {
        try { if (File.Exists(_filePath)) return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_filePath)) ?? []; }
        catch { }
        return [];
    }

    public static void Add(HistoryEntry entry)
    {
        var list = Load();
        list.Insert(0, entry);
        var settings = SettingsManager.Load();
        if (list.Count > settings.MaxHistoryEntries) list = list.Take(settings.MaxHistoryEntries).ToList();
        Save(list);
    }

    public static void Clear() => Save([]);

    private static void Save(List<HistoryEntry> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
```

---

## File: ZipWithPassword/SettingsManager.cs

```csharp
using System.IO;
using System.Text.Json;

namespace ZipWithPassword;

public class AppSettings
{
    public string DefaultOutputFolder  { get; set; } = string.Empty;
    public bool   ContextMenuInstalled { get; set; } = false;
    public int    MaxHistoryEntries    { get; set; } = 100;
}

public static class SettingsManager
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SecureStarzip", "settings.json");

    private static AppSettings? _cached;

    public static AppSettings Load()
    {
        if (_cached != null) return _cached;
        try { if (File.Exists(_filePath)) { _cached = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings(); return _cached; } }
        catch { }
        _cached = new AppSettings();
        return _cached;
    }

    public static void Save(AppSettings settings)
    {
        _cached = settings;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
```

---

## File: ZipWithPassword/ContextMenuInstaller.cs

```csharp
using System.Diagnostics;
using Microsoft.Win32;

namespace ZipWithPassword;

public static class ContextMenuInstaller
{
    private const string MenuLabel  = "Secure Starzip -- Zip with password...";
    private const string SubKeyName = "SecureStarzip";

    private static readonly string[] TargetPaths =
    [
        @"SOFTWARE\Classes\*\shell",
        @"SOFTWARE\Classes\Directory\shell",
        @"SOFTWARE\Classes\Directory\Background\shell",
    ];

    public static void Install()
    {
        string exePath = GetExePath();
        RegistryKey root = TryOpenHklmWritable() ?? Registry.CurrentUser;
        foreach (string basePath in TargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true) ?? root.CreateSubKey(basePath);
            if (shellKey is null) continue;
            using var menuKey = shellKey.CreateSubKey(SubKeyName);
            menuKey.SetValue("", MenuLabel);
            menuKey.SetValue("Icon", $"\"{exePath}\"");
            using var cmdKey = menuKey.CreateSubKey("command");
            bool isBackground = basePath.Contains("Background");
            cmdKey.SetValue("", isBackground ? $"\"{exePath}\" \"%V\"" : $"\"{exePath}\" \"%1\"");
        }
        SettingsManager.Load().ContextMenuInstalled = true;
        SettingsManager.Save(SettingsManager.Load());
    }

    public static void Uninstall()
    {
        RegistryKey root = TryOpenHklmWritable() ?? Registry.CurrentUser;
        foreach (string basePath in TargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true);
            shellKey?.DeleteSubKeyTree(SubKeyName, throwOnMissingSubKey: false);
        }
        var s = SettingsManager.Load(); s.ContextMenuInstalled = false; SettingsManager.Save(s);
    }

    public static bool IsInstalled()
    {
        try { using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Classes\*\shell\{SubKeyName}"); return key != null; }
        catch
        {
            try { using var key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Classes\*\shell\{SubKeyName}"); return key != null; }
            catch { return false; }
        }
    }

    private static string GetExePath()
        => Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Cannot determine executable path.");

    private static RegistryKey? TryOpenHklmWritable()
    {
        try { return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes", writable: true); }
        catch { return null; }
    }
}
```

---

## File: installer.wxs

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <Package Name="Secure Starzip"
           Manufacturer="DePaolo Consulting LLC"
           Version="1.1.0"
           UpgradeCode="A3F2C1D4-8B7E-4F5A-9C6D-2E1B0A3D4F5C"
           Language="1033"
           InstallerVersion="500"
           Scope="perMachine">

    <MajorUpgrade Schedule="afterInstallInitialize"
        DowngradeErrorMessage="A newer version of Secure Starzip is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <ui:WixUI Id="WixUI_InstallDir" />
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    <WixVariable Id="WixUILicenseRtf" Value="license.rtf" />
    <WixVariable Id="WixUIBannerBmp"  Value="installer_banner.bmp" />
    <WixVariable Id="WixUIDialogBmp"  Value="installer_dialog.bmp" />

    <Property Id="ARPURLINFOABOUT" Value="https://github.com/sstephen-arch/zip-with-password" />
    <Property Id="ARPHELPLINK"     Value="https://github.com/sstephen-arch/zip-with-password" />
    <Property Id="ARPPRODUCTICON"  Value="AppIconIco" />
    <Icon Id="AppIconIco" SourceFile="ZipWithPassword\app.ico" />

    <Feature Id="CoreFeature" Title="Secure Starzip" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentGroupRef Id="AppShortcuts" />
      <ComponentGroupRef Id="ContextMenuAnyFile" />
      <ComponentGroupRef Id="ContextMenuDirectory" />
      <ComponentGroupRef Id="ContextMenuDirBackground" />
    </Feature>
  </Package>

  <Fragment>
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFiles6432Folder">
        <Directory Id="INSTALLFOLDER" Name="SecureStarzip" />
      </Directory>
      <Directory Id="ProgramMenuFolder">
        <Directory Id="AppMenuFolder" Name="Secure Starzip" />
      </Directory>
    </Directory>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
      <Component Id="MainExe" Guid="B4E3D2C1-9A8F-4E5B-8D7E-3F2A1B0C4D5E">
        <File Id="SecureStarzipExe" Source="dist\ZipWithPassword.exe" KeyPath="yes" Name="SecureStarzip.exe" />
      </Component>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="AppShortcuts" Directory="AppMenuFolder">
      <Component Id="StartMenuShortcut" Guid="C5D4E3F2-1B2A-4E6C-8D9E-5A4B3C2D1E0F">
        <Shortcut Id="AppStartMenuShortcut" Name="Secure Starzip"
                  Description="Secure file archiving with AES-256 encryption"
                  Target="[INSTALLFOLDER]SecureStarzip.exe"
                  WorkingDirectory="INSTALLFOLDER" Icon="AppIconIco" />
        <RegistryValue Root="HKCU" Key="Software\DePaoloConsulting\SecureStarzip"
                       Name="StartMenuInstalled" Type="integer" Value="1" KeyPath="yes" />
        <RemoveFolder Id="RemoveAppMenuFolder" Directory="AppMenuFolder" On="uninstall" />
      </Component>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ContextMenuAnyFile" Directory="INSTALLFOLDER">
      <Component Id="CtxFile" Guid="D6E5F4A3-2C1B-5F7D-9E0F-6B5C4D3E2F1A">
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\*\shell\SecureStarzip" ForceDeleteOnUninstall="yes">
          <RegistryValue Type="string" Value="Secure Starzip -- Zip with password..." KeyPath="yes" />
          <RegistryValue Name="Icon" Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot;" />
        </RegistryKey>
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\*\shell\SecureStarzip\command">
          <RegistryValue Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot; &quot;%1&quot;" />
        </RegistryKey>
      </Component>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ContextMenuDirectory" Directory="INSTALLFOLDER">
      <Component Id="CtxDir" Guid="E7F6A5B4-3D2C-6A8E-0F1A-7C6D5E4F3A2B">
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\Directory\shell\SecureStarzip" ForceDeleteOnUninstall="yes">
          <RegistryValue Type="string" Value="Secure Starzip -- Zip with password..." KeyPath="yes" />
          <RegistryValue Name="Icon" Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot;" />
        </RegistryKey>
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\Directory\shell\SecureStarzip\command">
          <RegistryValue Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot; &quot;%1&quot;" />
        </RegistryKey>
      </Component>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ContextMenuDirBackground" Directory="INSTALLFOLDER">
      <Component Id="CtxDirBg" Guid="F8A7B6C5-4E3D-7B9F-1A2B-8D7E6F5A4B3C">
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\Directory\Background\shell\SecureStarzip" ForceDeleteOnUninstall="yes">
          <RegistryValue Type="string" Value="Secure Starzip -- Zip with password..." KeyPath="yes" />
          <RegistryValue Name="Icon" Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot;" />
        </RegistryKey>
        <RegistryKey Root="HKLM" Key="SOFTWARE\Classes\Directory\Background\shell\SecureStarzip\command">
          <RegistryValue Type="string" Value="&quot;[INSTALLFOLDER]SecureStarzip.exe&quot; &quot;%V&quot;" />
        </RegistryKey>
      </Component>
    </ComponentGroup>
  </Fragment>

</Wix>
```

---

## File: gen_installer_assets.py

```python
"""
Generates installer_banner.bmp (493x58) and installer_dialog.bmp (493x312).
Run: python gen_installer_assets.py  (requires Pillow)
"""
import math, sys
from PIL import Image, ImageDraw, ImageFont

BG_PRIMARY = (13,  15,  23)
BG_SURFACE = (18,  20,  32)
ACCENT     = (99, 102, 241)
TEXT_PRI   = (240, 241, 255)
TEXT_SEC   = (140, 145, 175)
GOLD       = (255, 210,  60)

def _font(path, size):
    for f in [path, "C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf",
              "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]:
        try: return ImageFont.truetype(f, size)
        except: pass
    return ImageFont.load_default()

def _bold(size):
    for p in ["C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/arialbd.ttf",
              "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]:
        try: return ImageFont.truetype(p, size)
        except: pass
    return ImageFont.load_default()

def draw_sun(d, cx, cy, r, n=8):
    ro = r * 1.75; ri = r * 1.15
    for i in range(n):
        a = math.pi*2*i/n - math.pi/2
        ox = cx + math.cos(a)*ro; oy = cy + math.sin(a)*ro
        a1 = a + math.pi/n; a2 = a - math.pi/n
        d.polygon([(ox,oy),(cx+math.cos(a1)*ri,cy+math.sin(a1)*ri),
                   (cx+math.cos(a2)*ri,cy+math.sin(a2)*ri)], fill=GOLD)
    d.ellipse([cx-r,cy-r,cx+r,cy+r], fill=GOLD)
    d.ellipse([cx-r*.55,cy-r*.55,cx+r*.55,cy+r*.55], fill=(255,235,130))

def make_banner():
    img = Image.new("RGB", (493, 58), BG_SURFACE); d = ImageDraw.Draw(img)
    d.rectangle([0,0,4,58], fill=ACCENT); d.rectangle([0,56,493,58], fill=ACCENT)
    draw_sun(d, 458, 29, 13)
    d.text((18,9),  "Secure Starzip", fill=TEXT_PRI, font=_bold(18))
    d.text((18,33), "Secure file archiving with AES-256 encryption", fill=TEXT_SEC, font=_font(None,11))
    img.save("installer_banner.bmp", "BMP"); print("  installer_banner.bmp  493x58")

def make_dialog():
    img = Image.new("RGB", (493, 312), BG_PRIMARY); d = ImageDraw.Draw(img)
    for x in range(170):
        t = x/170
        d.line([(x,0),(x,312)], fill=tuple(int(BG_PRIMARY[i]+(BG_SURFACE[i]-BG_PRIMARY[i])*t) for i in range(3)))
    d.rectangle([0,0,4,312], fill=ACCENT)
    draw_sun(d, 85, 130, 48)
    d.text((22,192), "Secure",  fill=TEXT_PRI, font=_bold(22))
    d.text((22,218), "Starzip", fill=ACCENT,   font=_bold(22))
    d.text((22,248), "AES-256 encryption",       fill=TEXT_SEC, font=_font(None,10))
    d.text((22,261), "Right-click context menu",  fill=TEXT_SEC, font=_font(None,10))
    d.text((22,274), "Windows 10 / 11",           fill=TEXT_SEC, font=_font(None,10))
    d.text((22,296), "v1.1.0  (c) 2026 DePaolo Consulting LLC", fill=(70,75,95), font=_font(None,9))
    for row in range(7):
        for col in range(9):
            dx=195+col*37; dy=55+row*38
            d.ellipse([dx-2,dy-2,dx+2,dy+2], fill=(35,38,58))
    img.save("installer_dialog.bmp", "BMP"); print("  installer_dialog.bmp  493x312")

if __name__ == "__main__":
    try: make_banner(); make_dialog(); print("Done.")
    except Exception as e: print(f"ERROR: {e}", file=sys.stderr); sys.exit(1)
```

---

## File: .github/workflows/build.yml

```yaml
name: Build — MSI + MSIX

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:
    inputs:
      build_msix:
        description: 'Also build MSIX for Windows Store?'
        type: boolean
        default: false

permissions:
  contents: read

jobs:
  build-msi:
    name: Build MSI
    runs-on: windows-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore NuGet packages
        run: dotnet restore ZipWithPassword/ZipWithPassword.csproj

      - name: Publish app (self-contained, single-file)
        run: |
          dotnet publish ZipWithPassword/ZipWithPassword.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=none `
            --output dist
        shell: pwsh

      - name: Generate installer assets
        run: |
          pip install Pillow --quiet
          python gen_installer_assets.py
        shell: pwsh

      - name: Install WiX v4
        run: dotnet tool install --global wix --version 4.0.5
        shell: pwsh

      - name: Add WiX UI extension
        run: wix extension add --global WixToolset.UI.wixext/4.0.5
        shell: pwsh

      - name: Build MSI installer
        run: |
          wix build installer.wxs `
            -ext WixToolset.UI.wixext `
            -o dist/SecureStarzip.msi
        shell: pwsh

      - name: Upload MSI artifact
        uses: actions/upload-artifact@v4
        with:
          name: SecureStarzip-MSI
          path: dist/SecureStarzip.msi
          retention-days: 30

      - name: Upload exe artifact
        uses: actions/upload-artifact@v4
        with:
          name: ZipWithPassword-exe
          path: dist/ZipWithPassword.exe
          retention-days: 30

  build-msix:
    name: Build MSIX (Windows Store)
    runs-on: windows-latest
    needs: build-msi
    if: ${{ github.event_name == 'workflow_dispatch' && inputs.build_msix == true }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Download exe artifact
        uses: actions/download-artifact@v4
        with:
          name: ZipWithPassword-exe
          path: dist
      - name: Assemble MSIX layout
        run: |
          New-Item -ItemType Directory -Force -Path msix-layout\Assets | Out-Null
          Copy-Item dist\ZipWithPassword.exe msix-layout\
          Copy-Item packaging\AppxManifest.xml msix-layout\
          Copy-Item packaging\assets\* msix-layout\Assets\
        shell: pwsh
      - name: Find MakeAppx
        id: sdk
        run: |
          $makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
            -Recurse -Filter makeappx.exe |
            Where-Object { $_.FullName -match 'x64' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName
          echo "path=$makeappx" >> $env:GITHUB_OUTPUT
        shell: pwsh
      - name: Package MSIX
        run: |
          & "${{ steps.sdk.outputs.path }}" pack /d msix-layout /p dist\SecureStarzip.msix /nv
        shell: pwsh
      - name: Upload MSIX artifact
        uses: actions/upload-artifact@v4
        with:
          name: SecureStarzip-MSIX
          path: dist/SecureStarzip.msix
          retention-days: 30
```

---

## Icon (app.ico)

Generate with this Python script (requires Pillow). Save output as `ZipWithPassword/app.ico`:

```python
import math
from PIL import Image, ImageDraw

GOLD=(255,200,40,255); GOLD_HL=(255,240,130,255)
METAL=(160,165,175,255); METAL_DARK=(70,75,88,255); METAL_LITE=(215,220,228,255)
BG_DARK=(13,15,23,255)

def make_icon(size):
    img=Image.new('RGBA',(size,size),(0,0,0,0)); d=ImageDraw.Draw(img); cx=cy=size/2
    d.rounded_rectangle([0,0,size-1,size-1],radius=int(size*0.14),fill=BG_DARK)
    ro=size*0.39; ri=size*0.255
    for i in range(8):
        a=math.tau*i/8-math.pi/2; half=math.tau/8*0.33
        d.polygon([(cx+math.cos(a)*ro,cy+math.sin(a)*ro),
                   (cx+math.cos(a-half)*ri,cy+math.sin(a-half)*ri),
                   (cx+math.cos(a+half)*ri,cy+math.sin(a+half)*ri)],fill=GOLD)
    r=size*0.215
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=GOLD)
    hr=r*0.48
    d.ellipse([cx-hr,cy-r*0.65,cx+hr*0.4,cy-r*0.08],fill=GOLD_HL)
    jaw_w=size*0.165; jaw_h=size*0.41; gap=size*0.305
    for side in(-1,1):
        x1,x2=(cx-gap-jaw_w,cx-gap) if side==-1 else (cx+gap,cx+gap+jaw_w)
        y1=cy-jaw_h/2; y2=cy+jaw_h/2
        d.rectangle([x1,y1,x2,y2],fill=METAL)
        ht=max(1,size*0.035)
        d.rectangle([x1,y1,x2,y1+ht],fill=METAL_LITE)
        d.rectangle([x1,y2-ht,x2,y2],fill=METAL_DARK)
        ew=max(1,size*0.04)
        d.rectangle([x1 if side==-1 else x2-ew,y1,(x1+ew) if side==-1 else x2,y2],fill=METAL_LITE)
        iw=max(1,jaw_w*0.22)
        if side==-1:
            d.rectangle([x2-iw,y1,x2,y2],fill=METAL_DARK)
            if size>=32:
                for t in range(4):
                    ty=y1+jaw_h*(t+1)/5; tw=max(1,int(size*0.018))
                    d.line([(x2-iw,ty),(x2,ty)],fill=METAL_LITE,width=tw)
        else:
            d.rectangle([x1,y1,x1+iw,y2],fill=METAL_DARK)
            if size>=32:
                for t in range(4):
                    ty=y1+jaw_h*(t+1)/5; tw=max(1,int(size*0.018))
                    d.line([(x1,ty),(x1+iw,ty)],fill=METAL_LITE,width=tw)
        if size>=32:
            br=max(1,size*0.045); bcx=(x1+x2)/2
            for ty_b in[y1+jaw_h*0.2,y1+jaw_h*0.8]:
                d.ellipse([bcx-br,ty_b-br,bcx+br,ty_b+br],fill=METAL_DARK)
                sr=max(1,br*0.4)
                d.ellipse([bcx-sr,ty_b-br*0.7,bcx+sr*0.3,ty_b-br*0.1],fill=METAL_LITE)
    return img

sizes=[256,128,64,48,32,16]; imgs=[make_icon(s) for s in sizes]
imgs[0].save('app.ico',format='ICO',append_images=imgs[1:],sizes=[(s,s) for s in sizes])
print("app.ico created")
```

---

## Key design decisions

- **No StartupUri** in App.xaml — window is created manually in `OnStartup` to support CLI args (`--install`, `--uninstall`, `<path>`)
- **Self-contained single-file exe** — no .NET runtime required on end-user machine
- **AES-256** via DotNetZip `EncryptionAlgorithm.WinZipAes256` — compatible with WinZip, 7-Zip
- **Context menu** writes to HKLM if elevated, falls back to HKCU silently
- **WiX v4.0.5 pinned** — v5+ requires OSMF fee; `wix extension add` uses `PackageName/Version` syntax (not `--version` flag)
- **History + Settings** stored as JSON in `%APPDATA%\SecureStarzip\`
- **Password generator** uses `RandomNumberGenerator` (CSPRNG), guarantees upper+lower+digit+special
- **Installer bitmaps** generated at build time by `gen_installer_assets.py` using Pillow — dark theme matching the app palette
