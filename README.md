# LionFire.Stride

I have .NET 5.0 + Blazor Server + Ultralig.ht working inside Stride (stride3d.net) game engine on Windows.  This repo doesn't have a complete working example (feel free to create one) but contains some key files.

![screenshot](screenshots/BrowserAndStride.png "Blazor Server in Stride")

## Stride + Ultralig.ht integration

Based off of: https://github.com/makotech222/Ultralight-Stride3d_Integration

## Stride UI fix

By default, as of now, Stride's postproessing effects are applied to the UI, which makes it look bad.

Refer to here for the full scoop: https://github.com/herocrab/StrideCleanUI

In short:

 1. Set UI to render to group 31
 2. Rework your compositor (I included my file FWIW: GraphicsCompositor.sdgfxcomp)

## Hosting ASP.NET Core (Blazor Server) alongside Stride

See StrideGameService.cs

 1. Create an ASP.NET Core application (.NET 5.0)
 2. In your ConfigureServices(IServicesCollection services) method, do  services.AddHostedService<StrideGameService>();

A more up to date copy of StrideGameService.cs may be here: 
https://github.com/lionfire/Core/blob/master/src/LionFire.Hosting.Stride/StrideGameService.cs
