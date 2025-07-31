# AGENTS Instructions

## Build requirements
- This project targets **.NET 9**. Install the .NET 9 SDK before running any `dotnet` commands.
- When working on Linux, use the `Release-Photino` or `Release-Web` configurations. The Windows build configurations (`*Windows`) require MAUI and only work on Windows.

## Running checks
- There are currently no unit tests. To verify the project builds, run:
  ```bash
  dotnet publish Desktop/Desktop.csproj -c Release-Photino -o ./publish/Photino-Linux
  ```
  or for the web version:
  ```bash
  dotnet publish Desktop/Desktop.csproj -c Release-Web -o ./publish/Web-Linux
  ```
- Ensure these commands succeed before committing changes.

