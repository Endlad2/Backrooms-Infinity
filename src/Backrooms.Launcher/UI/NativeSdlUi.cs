using System.Runtime.InteropServices;
using System.Text;
using SDL3;

namespace Backrooms.Launcher.UI;

public sealed class NativeSdlUi : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FRect
    {
        public float X;
        public float Y;
        public float W;
        public float H;

        public FRect(float x, float y, float w, float h) => (X, Y, W, H) = (x, y, w, h);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeColor
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public NativeColor(byte r, byte g, byte b, byte a) => (R, G, B, A) = (r, g, b, a);
    }

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_RenderTexture(IntPtr renderer, IntPtr texture, IntPtr srcRect, ref FRect dstRect);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_RenderFillRect(IntPtr renderer, ref FRect rect);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_RenderRect(IntPtr renderer, ref FRect rect);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_GetTextureSize(IntPtr texture, out float w, out float h);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_CreateTextureFromSurface(IntPtr renderer, IntPtr surface);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_DestroySurface(IntPtr surface);

    [DllImport("SDL3_image", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr IMG_LoadTexture(IntPtr renderer, [MarshalAs(UnmanagedType.LPUTF8Str)] string file);

    [DllImport("SDL3_ttf", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool TTF_Init();

    [DllImport("SDL3_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TTF_Quit();

    [DllImport("SDL3_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TTF_OpenFont([MarshalAs(UnmanagedType.LPUTF8Str)] string file, float pointSize);

    [DllImport("SDL3_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TTF_CloseFont(IntPtr font);

    [DllImport("SDL3_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TTF_RenderText_Blended(
        IntPtr font,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        nuint length,
        NativeColor foreground);

    private readonly IntPtr _renderer;
    private readonly Dictionary<int, IntPtr> _fonts = new();
    private readonly Dictionary<string, IntPtr> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _fontPath;
    private bool _ttfReady;

    public NativeSdlUi(IntPtr renderer, string fontPath)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        _ttfReady = TTF_Init();
        if (!_ttfReady)
            throw new InvalidOperationException($"SDL_ttf init failed: {SDL.GetError()}");
    }

    public IntPtr LoadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var cached) && cached != IntPtr.Zero)
            return cached;

        var texture = IMG_LoadTexture(_renderer, path);
        if (texture == IntPtr.Zero)
            return IntPtr.Zero;
        _textures[path] = texture;
        return texture;
    }

    public void DrawCover(IntPtr texture, int windowWidth, int windowHeight, double panX, double zoom)
    {
        if (texture == IntPtr.Zero || !SDL_GetTextureSize(texture, out var tw, out var th) || tw <= 0 || th <= 0)
            return;

        var scale = Math.Max(windowWidth / tw, windowHeight / th) * (float)zoom;
        var w = tw * scale;
        var h = th * scale;
        var x = (windowWidth - w) * 0.5f + (float)(panX * windowWidth);
        var y = (windowHeight - h) * 0.5f;
        var dst = new FRect(x, y, w, h);
        SDL_RenderTexture(_renderer, texture, IntPtr.Zero, ref dst);
    }

    public void DrawTexture(IntPtr texture, float x, float y, float w, float h)
    {
        if (texture == IntPtr.Zero) return;
        var dst = new FRect(x, y, w, h);
        SDL_RenderTexture(_renderer, texture, IntPtr.Zero, ref dst);
    }

    public void Fill(float x, float y, float w, float h, byte r, byte g, byte b, byte a = 255)
    {
        SDL.SetRenderDrawColor(_renderer, r, g, b, a);
        var rect = new FRect(x, y, w, h);
        SDL_RenderFillRect(_renderer, ref rect);
    }

    public void Outline(float x, float y, float w, float h, byte r, byte g, byte b, byte a = 255)
    {
        SDL.SetRenderDrawColor(_renderer, r, g, b, a);
        var rect = new FRect(x, y, w, h);
        SDL_RenderRect(_renderer, ref rect);
    }

    public (float Width, float Height) DrawText(string text, int size, float x, float y, byte r = 255, byte g = 255, byte b = 255, byte a = 255)
    {
        var font = GetFont(size);
        if (font == IntPtr.Zero || string.IsNullOrEmpty(text))
            return (0, 0);

        var surface = TTF_RenderText_Blended(font, text, (nuint)Encoding.UTF8.GetByteCount(text), new NativeColor(r, g, b, a));
        if (surface == IntPtr.Zero)
            return (0, 0);

        var texture = SDL_CreateTextureFromSurface(_renderer, surface);
        SDL_DestroySurface(surface);
        if (texture == IntPtr.Zero)
            return (0, 0);

        SDL_GetTextureSize(texture, out var w, out var h);
        DrawTexture(texture, x, y, w, h);
        SDL.DestroyTexture(texture);
        return (w, h);
    }

    public (float Width, float Height) MeasureText(string text, int size)
    {
        var font = GetFont(size);
        if (font == IntPtr.Zero || string.IsNullOrEmpty(text)) return (0, 0);
        var surface = TTF_RenderText_Blended(font, text, (nuint)Encoding.UTF8.GetByteCount(text), new NativeColor(255,255,255,255));
        if (surface == IntPtr.Zero) return (0, 0);
        var texture = SDL_CreateTextureFromSurface(_renderer, surface);
        SDL_DestroySurface(surface);
        if (texture == IntPtr.Zero) return (0, 0);
        SDL_GetTextureSize(texture, out var w, out var h);
        SDL.DestroyTexture(texture);
        return (w, h);
    }

    public bool Button(string text, int size, float x, float y, float w, float h, float mouseX, float mouseY, bool clicked)
    {
        var hovered = mouseX >= x && mouseX <= x + w && mouseY >= y && mouseY <= y + h;
        Fill(x, y, w, h, hovered ? (byte)35 : (byte)18, hovered ? (byte)35 : (byte)18, hovered ? (byte)35 : (byte)18, 230);
        Outline(x, y, w, h, hovered ? (byte)235 : (byte)170, hovered ? (byte)235 : (byte)170, hovered ? (byte)235 : (byte)170, 255);
        var m = MeasureText(text, size);
        DrawText(text, size, x + (w - m.Width) * 0.5f, y + (h - m.Height) * 0.5f);
        return hovered && clicked;
    }

    private IntPtr GetFont(int size)
    {
        if (_fonts.TryGetValue(size, out var font)) return font;
        font = TTF_OpenFont(_fontPath, size);
        _fonts[size] = font;
        return font;
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            if (texture != IntPtr.Zero) SDL.DestroyTexture(texture);
        _textures.Clear();

        foreach (var font in _fonts.Values)
            if (font != IntPtr.Zero) TTF_CloseFont(font);
        _fonts.Clear();

        if (_ttfReady)
        {
            TTF_Quit();
            _ttfReady = false;
        }
    }
}
