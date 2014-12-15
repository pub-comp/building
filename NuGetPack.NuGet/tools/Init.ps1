param($installPath, $toolsPath, $package)

$solutionNode = Get-Interface $dte.Solution ([EnvDTE80.Solution2])

$solutionItemsNode = $solutionNode.Projects | where-object { $_.ProjectName -eq ".NuGetPack" } | select -first 1

if (!$solutionItemsNode) {
	$solutionItemsNode = $solutionNode.AddSolutionFolder(".NuGetPack")
}

$solutionItemsProjectItems = Get-Interface $solutionItemsNode.ProjectItems ([EnvDTE.ProjectItems])

$rootDir = (Get-Item $installPath).parent.parent.fullname

$deploySource = join-path $installPath '\sln\'
$deployTarget = join-path $rootDir '\.NuGetPack\'

New-Item -ItemType Directory -Force -Path $deployTarget

ls $deploySource | foreach-object {
	
	$targetFile = join-path $deployTarget $_.Name
	
	Copy-Item $_.FullName $targetFile -Recurse -Force

	$solutionItemsProjectItems.AddFromFile($targetFile) > $null

} > $null
