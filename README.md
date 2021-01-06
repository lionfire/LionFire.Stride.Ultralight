# LionFire.Stride

I have Blazor Server + Ultralig.ht working inside Stride (stride3d.net) game engine.  This repo doesn't have a working example but contains some relevant files.

![screenshot](screenshots/BrowserAndStride.png "Blazor Server in Stride")

## Stride + Ultralig.ht integration

Based off of: https://github.com/makotech222/Ultralight-Stride3d_Integration

## Stride UI fix

By default, as of now, Stride's postproessing effects are applied to the UI, which makes it look bad.

Based off of: https://github.com/herocrab/StrideCleanUI

## Hosting ASP.NET Core (Blazor Server) alongside Stride

 1. Create an ASP.NET Core application (.NET 5.0)
 2. IServicesCollection.AddHostedService<StrideGameService>()  
