// Based on https://github.com/makotech222/Ultralight-Stride3d_Integration (MIT License)
// License for this file: MIT License
// Author: Jared Thirsk
// A more up to date version of this file may exist in a private repo.  If interested in a newer version, contact me.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LionFire.Dependencies;
using Microsoft.Extensions.Logging;
using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering.Sprites;
using Stride.UI.Controls;
using ImpromptuNinjas.UltralightSharp.Enums;
using ImpromptuNinjas.UltralightSharp.Safe;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;

namespace LionFire.Stride3D.UI
{
    /// <remarks>
    /// Sets up Ultralight to draws to Grid > Image "img"
    /// Override Start() and Update() and call the base methods
    /// </remarks>
    public class UltralightUIScript : SyncScript
    {
        #region (Static)

        /// <summary>
        /// Should be only one renderer per Game.
        /// </summary>
        protected static Renderer renderer;

        #endregion

        #region Dependencies

        private static ILogger Logger { get; set; }

        #endregion

        #region Fields

        /// <summary>
        /// View created by Ultralight.
        /// </summary>
        protected View View { get; set; }

        protected Session session;
        protected Texture texture;
        protected SpriteFromTexture sprite;
        protected ImageElement imageElement;
        protected uint width;
        protected uint height;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Full path to directory containing html files.
        /// </summary>
        [DataMemberIgnore]
        public string AssetDirectory { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "wwwroot");

        /// <summary>
        /// File path to main html file. Should be inside the AssetDirectory folder.
        /// </summary>
        [DataMemberIgnore]
        public string SplashUrl { get; set; } = "http://google.com"; // file:///index.html

        [DataMemberIgnore]
        public string StartUrl { get; set; } = "http://localhost:5000/";

        #endregion Properties

        #region Construction and Destruction

        static UltralightUIScript()
        {
            Logger ??= DependencyContext.Current.GetService<ILogger<UltralightUIScript>>();
        }

        public UltralightUIScript()
        {
        }

        protected void InitUltralight()
        {
            Ultralight.SetLogger(new Logger { LogMessage = LoggerCallback });

            using var cfg = new Config();

            var cachePath = Path.Combine(AssetDirectory, "Cache");
            cfg.SetCachePath(cachePath);

            var resourcePath = Path.Combine(AssetDirectory, "resources");
            cfg.SetResourcePath(resourcePath);

            cfg.SetUseGpuRenderer(false);
            cfg.SetEnableImages(true);
            cfg.SetEnableJavaScript(true);

            AppCore.EnablePlatformFontLoader();
            AppCore.EnablePlatformFileSystem(AssetDirectory);
            renderer = new Renderer(cfg);

        }

        private void ToggleVisibiilty()
        {
            Visible ^= true;
            imageElement.Visibility = Visible ? Stride.UI.Visibility.Visible : Stride.UI.Visibility.Hidden;
            imageElement.CanBeHitByUser = Visible;

        }

        ~UltralightUIScript()
        {
            View?.Dispose();
            renderer?.Dispose();
        }

        #endregion

        #region Methods

        public override void Start()
        {
            base.Start();
            var uiComponent = Entity.Get<UIComponent>();

            if (uiComponent == null) { Logger.LogError($"{this.GetType().FullName} script must be installed on the same Entity as UIComponent"); return; }

            //var gridElement = uiComponent.Page.RootElement.VisualChildren.FirstOrDefault() as Stride.UI.Panels.Grid;

            imageElement = uiComponent.Page.RootElement.VisualChildren.OfType<ImageElement>().FirstOrDefault() as ImageElement;
            if (imageElement == null) { Logger.LogError($"Failed to find image element.  The first ImageElement in VisualChildren will be used."); return; }

            imageElement.TouchUp += ImageElement_TouchUp;
            imageElement.TouchDown += ImageElement_TouchDown;
            imageElement.TouchEnter += ImageElement_TouchEnter;
            imageElement.MouseOverStateChanged += ImageElement_MouseOverStateChanged;

            Logger.LogInformation($"Drawing to image: {imageElement.Name}");

            width = (uint)uiComponent.Resolution.X;
            height = (uint)uiComponent.Resolution.Y;

            texture = Texture.New2D(this.GraphicsDevice, (int)width, (int)height, Stride.Graphics.PixelFormat.B8G8R8A8_UNorm_SRgb, TextureFlags.ShaderResource | TextureFlags.RenderTarget);
            sprite = new SpriteFromTexture();

            if (renderer == null) { InitUltralight(); }

            session = new Session(renderer, false, "");
            View = new View(renderer, width, height, true, session);
            View.LoadUrl(SplashUrl);

            View.SetFinishLoadingCallback((data, caller, frameId, isMainFrame, url) =>
            {
                loadedSplashUrl = true;
                Logger.LogInformation($"Finished loading: {url}");
            }, default);

            while (!loadedSplashUrl)
            {
                renderer.Update();
                renderer.Render();
            }
        }

        private void ImageElement_MouseOverStateChanged(object sender, Stride.UI.PropertyChangedArgs<Stride.UI.MouseOverState> e)
        {
            Logger.LogInformation($"MouseOverStateChanged {e.NewValue}");
        }

        private void ImageElement_TouchEnter(object sender, Stride.UI.TouchEventArgs e)
            => Logger.LogInformation("TouchEnter " + e.ScreenPosition);

        private void ImageElement_TouchDown(object sender, Stride.UI.TouchEventArgs e)
        {
            Logger.LogInformation($"TouchDown {e.ScreenPosition} ({(int)(e.ScreenPosition.X * width)},{(int)(e.ScreenPosition.Y * height)}) {(Input.Mouse.DownButtons.Any() ? Input.Mouse.DownButtons.Select(m => m.ToString()).Aggregate((x, y) => $"{x},{y}") : "")}");

            pendingInputEvents.MouseEvents.Enqueue(new MouseEvent(MouseEventType.MouseDown, (int)(e.ScreenPosition.X * width), (int)(e.ScreenPosition.Y * height), MouseButton.Left));
        }

        private void ImageElement_TouchUp(object sender, Stride.UI.TouchEventArgs e)
        {
            Logger.LogInformation($"TouchUp {e.ScreenPosition} ({(int)(e.ScreenPosition.X * width)},{(int)(e.ScreenPosition.Y * height)})");
            pendingInputEvents.MouseEvents.Enqueue(new MouseEvent(MouseEventType.MouseUp, (int)(e.ScreenPosition.X * width), (int)(e.ScreenPosition.Y * height), MouseButton.Left));
        }

        bool loadedSplashUrl = false;
        bool startedLoadingStartUrl = false;

        bool didHi = false;
        DateTime startTime = DateTime.UtcNow;

        public override void Update()
        {
            if (renderer == null) return;

            if (!startedLoadingStartUrl && DateTime.UtcNow - startTime > TimeSpan.FromSeconds(7))
            {
                View.LoadUrl(StartUrl);
                startedLoadingStartUrl = true;
            }

            if (!didHi)
            {
                didHi = true;
                try
                {
                    //var result = View.EvaluateScript($"console.log('hi')", out string exception);
                    var result = View.EvaluateScript($"console.log('hi ' + (2+2)); 2+2;", out string exception);
                    Logger.LogInformation("Javascript returned: " + result);
                    if (!string.IsNullOrEmpty(exception))
                    {
                        Logger.LogError("Javascript returned exception: " + exception);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Javascript threw exception");
                }
            }

            FireInputs(pendingInputEvents);

            if (Input.PressedKeys.Contains(Stride.Input.Keys.F10))
            {
                ToggleVisibiilty();
            }

            renderer.Update();
            renderer.Render();

            var surface = View.GetSurface();
            var bitmap = surface.GetBitmap();
            var pixels = bitmap.LockPixels();

            DataPointer dataPointer = new DataPointer((IntPtr)pixels, (int)bitmap.GetHeight() * (int)bitmap.GetWidth() * (int)bitmap.GetBpp());
            texture.SetData(this.Game.GraphicsContext.CommandList, dataPointer);
            sprite.Texture = texture;
            imageElement.Source = sprite;

            bitmap.UnlockPixels();
            bitmap.Dispose();
        }

        bool Visible { get; set; } = true;

        ComponentUI pendingInputEvents = new ComponentUI();
        public void FireInputs(ComponentUI ui)
        {
            
            while (pendingInputEvents.MouseEvents.TryDequeue(out var e))
            {
                View.FireMouseEvent(e);
            }
            // TODO:
            //foreach (var e in scrollEvents)
            //{
            //    View.FireScrollEvent(e);
            //}
            //foreach (var e in keyEvents)
            //{
            //    View.FireKeyEvent(e);
            //}
        }

        #endregion Methods

        #region Classes

        public class ComponentUI
        {
            public ConcurrentQueue<MouseEvent> MouseEvents { get; set; } = new ConcurrentQueue<MouseEvent>();
            public ConcurrentQueue<KeyEvent> KeyboardEvents { get; set; } = new ConcurrentQueue<KeyEvent>();
            public ConcurrentQueue<ScrollEvent> ScrollEvents { get; set; } = new ConcurrentQueue<ScrollEvent>();
        }

        #endregion

        #region Logging

        protected static void LoggerCallback(ImpromptuNinjas.UltralightSharp.Enums.LogLevel logLevel, string msg)
        {
            var microsoftLogLevel = logLevel switch
            {
                ImpromptuNinjas.UltralightSharp.Enums.LogLevel.Error => LogLevel.Error,
                ImpromptuNinjas.UltralightSharp.Enums.LogLevel.Warning => LogLevel.Warning,
                ImpromptuNinjas.UltralightSharp.Enums.LogLevel.Info => LogLevel.Information,
                _ => LogLevel.Error,
            };
            Logger.Log(microsoftLogLevel, msg);
        }

        #endregion
    }
}