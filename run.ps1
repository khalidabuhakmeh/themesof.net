taskkill /im dotnet.exe /f
taskkill /im msbuild.exe /f

pushd src/themesof.net

try {
    dotnet build /t:rebuild /nologo
    if( $LASTEXITCODE -eq 0 ) {
        clsa ; dotnet run --no-build
    }
}
finally {
    popd
}
