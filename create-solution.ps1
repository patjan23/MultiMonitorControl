# Create WPF Solution in .NET 6
$solutionName = "MultiMonitorControl"
$projectName = "MultiMonitorControl"

Write-Host "Creating WPF solution structure for $solutionName..." -ForegroundColor Green

# Create root directory
New-Item -ItemType Directory -Force -Path $solutionName
Set-Location $solutionName

# Create solution file
dotnet new sln -n $solutionName

# Create WPF project
dotnet new wpf -n $projectName -f net6.0

# Create directory structure
New-Item -ItemType Directory -Force -Path "$projectName/Models"
New-Item -ItemType Directory -Force -Path "$projectName/Properties"
New-Item -ItemType Directory -Force -Path "$projectName/Resources"

# Add project to solution
dotnet sln add "$projectName/$projectName.csproj"

# Create the required files with basic templates

# Models/MonitorInfo.cs
$monitorInfoContent = @"
using System;

namespace $projectName.Models
{
    public class MonitorInfo
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public bool IsPrimary { get; set; }
        
        public override string ToString()
        {
            return `$"{Name} ({Width}x{Height})";
        }
    }
}
"@
Set-Content -Path "$projectName/Models/MonitorInfo.cs" -Value $monitorInfoContent

# Models/MonitorProfile.cs
$monitorProfileContent = @"
using System;
using System.Collections.Generic;

namespace $projectName.Models
{
    public class MonitorProfile
    {
        public string ProfileName { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<MonitorInfo> Monitors { get; set; } = new List<MonitorInfo>();
        
        public MonitorProfile()
        {
            CreatedDate = DateTime.Now;
        }
    }
}
"@
Set-Content -Path "$projectName/Models/MonitorProfile.cs" -Value $monitorProfileContent

# Properties/AssemblyInfo.cs
$assemblyInfoContent = @"
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]
"@
Set-Content -Path "$projectName/Properties/AssemblyInfo.cs" -Value $assemblyInfoContent

# Create empty Resources/monitor.ico file (placeholder)
New-Item -ItemType File -Path "$projectName/Resources/monitor.ico" -Force

# Update MainWindow.xaml with basic structure
$mainWindowXamlContent = @"
<Window x:Class=`"$projectName.MainWindow`"
        xmlns=`"http://schemas.microsoft.com/winfx/2006/xaml/presentation`"
        xmlns:x=`"http://schemas.microsoft.com/winfx/2006/xaml`"
        xmlns:d=`"http://schemas.microsoft.com/expression/blend/2008`"
        xmlns:mc=`"http://schemas.openxmlformats.org/markup-compatibility/2006`"
        xmlns:local=`"clr-namespace:$projectName`"
        mc:Ignorable=`"d`"
        Title=`"Multi Monitor Control`" Height=`"450`" Width=`"800`">
    <Grid>
        <TextBlock Text=`"Multi Monitor Control Application`" 
                   HorizontalAlignment=`"Center`"
                   VerticalAlignment=`"Center`"
                   FontSize=`"20`"
                   FontWeight=`"Bold`"/>
    </Grid>
</Window>
"@
Set-Content -Path "$projectName/MainWindow.xaml" -Value $mainWindowXamlContent

# Update MainWindow.xaml.cs
$mainWindowCsContent = @"
using System.Windows;

namespace $projectName
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
"@
Set-Content -Path "$projectName/MainWindow.xaml.cs" -Value $mainWindowCsContent

# Update App.xaml
$appXamlContent = @"
<Application x:Class=`"$projectName.App`"
             xmlns=`"http://schemas.microsoft.com/winfx/2006/xaml/presentation`"
             xmlns:x=`"http://schemas.microsoft.com/winfx/2006/xaml`"
             xmlns:local=`"clr-namespace:$projectName`"
             StartupUri=`"MainWindow.xaml`">
    <Application.Resources>
         
    </Application.Resources>
</Application>
"@
Set-Content -Path "$projectName/App.xaml" -Value $appXamlContent

# Update App.xaml.cs
$appCsContent = @"
using System.Windows;

namespace $projectName
{
    public partial class App : Application
    {
    }
}
"@
Set-Content -Path "$projectName/App.xaml.cs" -Value $appCsContent

Write-Host "✓ Solution structure created successfully!" -ForegroundColor Green
Write-Host "✓ Directory structure:" -ForegroundColor Yellow



Write-Host "`nTo build and run the solution:" -ForegroundColor Cyan
Write-Host "  cd $solutionName" -ForegroundColor White
Write-Host "  dotnet build" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White

Set-Location ..