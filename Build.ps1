$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $fx = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
    & "$fx\csc.exe" /nologo /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /target:winexe /codepage:65001 /out:日序.exe /resource:TravelClock.mesh.gz,TravelClock.mesh.gz /resource:Station.mesh.gz,Station.mesh.gz /resource:Orbit.mesh.gz,Orbit.mesh.gz /resource:Heritage.mesh.gz,Heritage.mesh.gz /resource:Flags.zip,Flags.zip /resource:Flag-Attribution.txt,Flag-Attribution.txt /resource:Cover.jpg,Cover.jpg /resource:SanmuMark.png,SanmuMark.png /resource:Dashboard.xaml,Dashboard.xaml /resource:Surfaces.xaml,Surfaces.xaml /resource:Strings.tsv,Strings.tsv /resource:Currencies.tsv,Currencies.tsv /resource:Places.tsv,Places.tsv /resource:Offsets.gz,Offsets.gz /resource:LICENSE,LICENSE /resource:Lucide-LICENSE.txt /resource:Unicode-LICENSE.txt "/r:$fx\WPF\PresentationFramework.dll" "/r:$fx\WPF\PresentationCore.dll" "/r:$fx\WPF\WindowsBase.dll" "/r:$fx\System.Xaml.dll" DayDesk.cs RoundedSurfaces.cs ControlMotion.cs Payroll.cs TodoTasks.cs Language.cs Pages.cs Compact.cs WidgetGlass.cs ClockFlags.cs WorldCatalog.cs InstrumentArt.cs DialDesign.cs TravelClock.cs TravelClockFace.cs
    if ($LASTEXITCODE -ne 0) { throw '构建失败' }
} finally { Pop-Location }
