$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $fx = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
    & "$fx\csc.exe" /nologo /r:System.Windows.Forms.dll /target:winexe /codepage:65001 /out:日序.exe /resource:Cover.jpg,Cover.jpg /resource:Dashboard.xaml,Dashboard.xaml /resource:Places.tsv,Places.tsv /resource:Offsets.gz,Offsets.gz /resource:LICENSE,LICENSE /resource:Lucide-LICENSE.txt /resource:Unicode-LICENSE.txt "/r:$fx\WPF\PresentationFramework.dll" "/r:$fx\WPF\PresentationCore.dll" "/r:$fx\WPF\WindowsBase.dll" "/r:$fx\System.Xaml.dll" DayDesk.cs Pages.cs Compact.cs WorldCatalog.cs InstrumentArt.cs
    if ($LASTEXITCODE -ne 0) { throw '构建失败' }
} finally { Pop-Location }
