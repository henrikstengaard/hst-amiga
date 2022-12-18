# package version
$changes = git status -s
$commitCount = [int32](git rev-list --count HEAD)

# increase commit count, if branch has uncommitted changes
if ($changes -notmatch '^\s*$')
{
	$commitCount++
}

$packageVersion = (Select-Xml -Path ./Directory.Build.props -XPath '/Project/PropertyGroup/PackageVersion').Node.InnerXML
$assemblyVersion = $packageVersion -replace '^(.*)\.\d+.*$', "`$1.$commitCount.0"
$packageVersion = $packageVersion -replace '^(.*)\.\d+.*$', "`$1.$commitCount-prerelease"

# restore packages
dotnet restore

# build
dotnet build --configuration Debug -p:Version=$packageVersion -p:AssemblyVersion=$assemblyVersion -p:FileVersion=$assemblyVersion

# test
dotnet test --configuration Debug --no-build --verbosity normal --filter Category!=Integration
      
# pack
dotnet pack --configuration Debug --no-build --output packages -p:Version=$packageVersion -p:PackageVersion=$packageVersion
