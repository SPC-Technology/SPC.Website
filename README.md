# SPC Technology Website

Blazor Web App (`.NET 8`) for the new SPC Technology marketing site, positioned as:

`Phoebus ERP by SPC Technology`

The site is intentionally simple and maintainable. It focuses on product positioning, lead generation, company credibility, support access, and contact information.

## Stack

- `.NET 8`
- `Blazor Web App`
- Server-side interactive rendering
- Plain Razor components and CSS
- `SendGrid` for demo request email delivery

## Current Pages

- `/` Home
- `/downloads`
- `/modules`
- `/services`
- `/support`
- `/about`
- `/contact`
- `/demo`

## Project Structure

- [Program.cs](/mnt/c/SPC/spc-website-2026/Program.cs): app startup, DI, demo request API endpoint
- [Controllers](/mnt/c/SPC/spc-website-2026/Controllers): controller-based APIs
- [Pages](/mnt/c/SPC/spc-website-2026/Pages): site pages
- [Components](/mnt/c/SPC/spc-website-2026/Components): shared UI components
- [Layout](/mnt/c/SPC/spc-website-2026/Layout): site layout
- [wwwroot/css/site.css](/mnt/c/SPC/spc-website-2026/wwwroot/css/site.css): global styling
- [wwwroot/images](/mnt/c/SPC/spc-website-2026/wwwroot/images): site images, logos, and reused legacy assets

## Run Locally

From this folder:

```bash
dotnet run
```

If you are running from WSL with the Windows .NET SDK:

```bash
WIN_PROJ="$(wslpath -w '/mnt/c/SPC/spc-website-2026/SPC.Website.csproj')"
'/mnt/c/Program Files/dotnet/dotnet.exe' run --project "$WIN_PROJ"
```

Default local URLs:

- `http://localhost:7002`

## Build

Standard:

```bash
dotnet build
```

WSL with Windows SDK:

```bash
WIN_PROJ="$(wslpath -w '/mnt/c/SPC/spc-website-2026/SPC.Website.csproj')"
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$WIN_PROJ" -v minimal
```

## Hosting as a Service

The app is configured to support both:

- Windows Service hosting
- Linux `systemd` hosting

Startup is wired in [Program.cs](/mnt/c/SPC/spc-website-2026/Program.cs) with:

- `UseWindowsService()`
- `UseSystemd()`

### Linux systemd

Example unit file:

- [deploy/linux/spc-website.service](/mnt/c/SPC/spc-website-2026/deploy/linux/spc-website.service)

Typical deployment flow:

1. Publish the app to a server directory such as `/opt/spc-website`
2. Copy the unit file to `/etc/systemd/system/spc-website.service`
3. Adjust `WorkingDirectory`, `ExecStart`, `User`, and environment values if needed
4. Enable and start the service

Example commands:

```bash
sudo systemctl daemon-reload
sudo systemctl enable spc-website
sudo systemctl start spc-website
sudo systemctl status spc-website
```

Default service URL:

- `http://0.0.0.0:7002`

### Windows Service

Example install script:

- [deploy/windows/install-service.ps1](/mnt/c/SPC/spc-website-2026/deploy/windows/install-service.ps1)

Typical deployment flow:

1. Publish the app for Windows
2. Confirm `SPC.Website.exe` exists in the publish folder
3. Run the PowerShell script as Administrator

Example publish command:

```powershell
dotnet publish .\SPC.Website.csproj -c Release -r win-x64 --self-contained false
```

The script creates a service named `SPCWebsite`.

## Demo Request Form

The demo request flow is implemented with:

- [Pages/Demo.razor](/mnt/c/SPC/spc-website-2026/Pages/Demo.razor)
- [Models/DemoRequestModel.cs](/mnt/c/SPC/spc-website-2026/Models/DemoRequestModel.cs)
- [Services/DemoRequestEmailService.cs](/mnt/c/SPC/spc-website-2026/Services/DemoRequestEmailService.cs)

The form posts to:

- `POST /api/demo-request`

Behavior:

- validates required fields
- uses a honeypot field for simple spam protection
- sends the request to sales via SendGrid
- returns a success response to the form

## Configuration

Set SendGrid values in [appsettings.json](/mnt/c/SPC/spc-website-2026/appsettings.json):

```json
"SendGrid": {
  "ApiKey": "",
  "FromEmail": "",
  "FromName": "",
  "ToEmail": ""
}
```

For the API key, the app first checks the `SENDGRID_API_KEY` environment variable.
If that variable is not set, it falls back to `SendGrid:ApiKey` in `appsettings.json`.

Recommended production setup:

```bash
export SENDGRID_API_KEY="your-sendgrid-api-key"
```

Configure the public downloads root in [appsettings.json](/mnt/c/SPC/spc-website-2026/appsettings.json):

```json
"Downloads": {
  "RootPath": "D:\\PublicDownloads",
  "Title": "Public Downloads",
  "AllowedExtensions": [],
  "UploadApiKey": "your-machine-to-machine-key",
  "UploadMaxRequestBodyBytes": 1073741824
}
```

Platform-specific examples for `Downloads:RootPath`:

- Windows: `D:\\PublicDownloads`
- Linux: `/srv/spc/downloads`
- macOS: `/Users/Shared/spc-downloads`

You can also override the root path with an environment variable:

- `Downloads__RootPath`

The downloads area is public and filesystem-backed:

- `/downloads`
- `/downloads/{folder path}`
- `GET /downloads/file/{file path}`

Upload API for other applications:

- `POST /api/upload`
- implemented in [DownloadsController.cs](/mnt/c/SPC/spc-website-2026/Controllers/DownloadsController.cs)
- Content type: `multipart/form-data`
- Header: `x-api-key`
- Form field `path`: optional target folder/subfolder under the downloads root
- Form field `overwrite`: optional `true` or `false`
- Form files: one or more files

Behavior:

- uploads are streamed to disk
- target folders are created automatically if needed
- files are restricted to the configured downloads root
- existing files are rejected unless `overwrite=true`

Example with `curl`:

```bash
curl -X POST "http://localhost:7002/api/upload" \
  -H "x-api-key: your-machine-to-machine-key" \
  -F "path=releases/v1" \
  -F "overwrite=false" \
  -F "files=@./PhoebusSetup.exe" \
  -F "files=@./ReleaseNotes.pdf"
```

Example success response:

```json
{
  "message": "Files uploaded successfully.",
  "path": "releases/v1",
  "files": [
    {
      "name": "PhoebusSetup.exe",
      "relativePath": "releases/v1/PhoebusSetup.exe",
      "sizeBytes": 104857600,
      "extension": ".exe",
      "lastModifiedUtc": "2026-03-17T18:30:00+00:00"
    }
  ]
}
```

Set the default HTTP bind URL in the `Kestrel` section if needed:

```json
"Kestrel": {
  "Endpoints": {
    "Http": {
      "Url": "http://0.0.0.0:7002"
    }
  }
}
```

## Design Notes

- No external UI framework is used.
- Styling is centralized in [site.css](/mnt/c/SPC/spc-website-2026/wwwroot/css/site.css).
- The site currently uses a dark visual direction inspired by the legacy SPC website.
- Legacy brand assets, staff images, and customer logos were reused from the older SPC website source where appropriate.
- The downloads implementation is designed to run on Windows, Linux, and macOS because it uses platform-neutral .NET filesystem APIs and relative-path normalization.
- Service-hosting templates are included for Windows and Linux. macOS can run the app normally with `dotnet SPC.Website.dll` or be wrapped later with `launchd` if needed.

## Legacy Asset Source

Some content and visual assets were pulled from the older SPC website project located at:

`C:\Users\tung\source\repos\SPC-Technology\SPC-WEB-SERVER-2023`

These include:

- company/about content
- staff photos
- customer logos
- selected background and brand images

## Notes for Future Updates

- Home is the main product positioning page.
- There is no standalone `/phoebus` page anymore.
- Customer logos and names on the homepage are based on available legacy assets.
- If new product screenshots are available, update the homepage hero images in [wwwroot/images](/mnt/c/SPC/spc-website-2026/wwwroot/images).
