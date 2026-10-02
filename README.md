# DBM Select

"Where your best shots get chosen."

## Build for macOS

Install the .NET 8 SDK on a Mac, then run these commands from the project
directory:

```sh
chmod +x build_mac.sh
./build_mac.sh
```

The script targets the architecture of the Mac on which it runs (`osx-arm64`
for Apple silicon or `osx-x64` for Intel). To choose a target explicitly, run
`./build_mac.sh osx-arm64` or `./build_mac.sh osx-x64`. It creates the `.app`
bundle and `.dmg` installer in `bin/Distribution`.

To publish the app without creating a `.app` bundle or `.dmg`, run:

```sh
dotnet publish dbm-select.csproj -c Release -r osx-arm64 --self-contained
```

Replace `osx-arm64` with `osx-x64` for Intel Macs. The publish command can also
run on Windows, but the packaging script uses macOS tools and must run on a Mac.
The included ad-hoc signature is intended for local testing; distribution
outside your organization generally requires Developer ID signing and
notarization.
