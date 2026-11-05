$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$source = Get-Content -LiteralPath (Join-Path $repository 'SoftcomSupportAutomation\Program.cs') -Raw -Encoding UTF8
$start = $source.IndexOf('        private T RunTimedPhase<T>(')
$end = $source.IndexOf('        private void CaptureConfigurationTabs(', $start)
$method = $source.Substring($start, $end-$start)
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'WorkflowProbe.template.cs') -Raw -Encoding UTF8
$probe = $template.Replace('__RUN_WORKFLOW__', $method)
Add-Type -TypeDefinition $probe -ReferencedAssemblies System,System.Windows.Forms,System.Core
[WorkflowProbe]::RunAll()
