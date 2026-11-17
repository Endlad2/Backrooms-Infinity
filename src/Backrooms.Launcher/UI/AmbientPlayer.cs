using System.Runtime.InteropServices;
using SDL3;

namespace Backrooms.Launcher.UI;

public sealed class AmbientPlayer : IDisposable
{
    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool MIX_Init();

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void MIX_Quit();

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr MIX_CreateMixerDevice(uint deviceId, IntPtr spec);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void MIX_DestroyMixer(IntPtr mixer);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr MIX_LoadAudio(IntPtr mixer, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.I1)] bool predecode);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void MIX_DestroyAudio(IntPtr audio);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr MIX_CreateTrack(IntPtr mixer);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void MIX_DestroyTrack(IntPtr track);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool MIX_SetTrackAudio(IntPtr track, IntPtr audio);

    [DllImport("SDL3_mixer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool MIX_PlayTrack(IntPtr track, uint options);

    private const uint DefaultPlaybackDevice = 0xFFFFFFFFu;
    private IntPtr _mixer;
    private IntPtr _audio;
    private IntPtr _track;
    private string? _current;
    private bool _initialized;

    public AmbientPlayer()
    {
        _initialized = MIX_Init();
        if (!_initialized) return;
        _mixer = MIX_CreateMixerDevice(DefaultPlaybackDevice, IntPtr.Zero);
        if (_mixer == IntPtr.Zero)
        {
            MIX_Quit();
            _initialized = false;
        }
    }

    public void Play(string path)
    {
        if (!_initialized || _mixer == IntPtr.Zero || string.Equals(path, _current, StringComparison.OrdinalIgnoreCase))
            return;
        _current = path;
        ReleaseTrack();
        if (!File.Exists(path)) return;

        _audio = MIX_LoadAudio(_mixer, path, predecode: false);
        if (_audio == IntPtr.Zero) return;
        _track = MIX_CreateTrack(_mixer);
        if (_track == IntPtr.Zero) return;
        if (!MIX_SetTrackAudio(_track, _audio)) return;
        MIX_PlayTrack(_track, 0);
    }

    private void ReleaseTrack()
    {
        if (_track != IntPtr.Zero)
        {
            MIX_DestroyTrack(_track);
            _track = IntPtr.Zero;
        }
        if (_audio != IntPtr.Zero)
        {
            MIX_DestroyAudio(_audio);
            _audio = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        ReleaseTrack();
        if (_mixer != IntPtr.Zero)
        {
            MIX_DestroyMixer(_mixer);
            _mixer = IntPtr.Zero;
        }
        if (_initialized)
        {
            MIX_Quit();
            _initialized = false;
        }
    }
}
