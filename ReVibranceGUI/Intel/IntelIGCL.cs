using System;
using System.Runtime.InteropServices;

namespace ReVibranceGUI.Intel
{
    public enum ctl_result_t : int
    {
        CTL_RESULT_SUCCESS = 0,
        // Other error codes omitted for brevity
    }

    [Flags]
    public enum ctl_init_flags_t : uint
    {
        CTL_INIT_FLAG_USE_LEVEL_ZERO = (1 << 0),
        CTL_INIT_FLAG_IGSC_FUL = (1 << 1)
    }

    public enum ctl_video_processing_feature_t : uint
    {
        CTL_VIDEO_PROCESSING_FEATURE_STANDARD_COLOR_CORRECTION = 5
    }

    public enum ctl_property_value_type_t : uint
    {
        CTL_PROPERTY_VALUE_TYPE_BOOL = 0,
        CTL_PROPERTY_VALUE_TYPE_FLOAT = 1,
        CTL_PROPERTY_VALUE_TYPE_INT32 = 2,
        CTL_PROPERTY_VALUE_TYPE_UINT32 = 3,
        CTL_PROPERTY_VALUE_TYPE_ENUM = 4,
        CTL_PROPERTY_VALUE_TYPE_CUSTOM = 5,
        CTL_PROPERTY_VALUE_TYPE_MAX
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_version_info_t
    {
        public uint Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_application_id_t
    {
        public uint Data1;
        public ushort Data2;
        public ushort Data3;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_init_args_t
    {
        public uint Size;
        public byte Version;
        public ctl_version_info_t AppVersion;
        public ctl_init_flags_t flags;
        public ctl_version_info_t SupportedVersion;
        public ctl_application_id_t ApplicationUID;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct ctl_property_t
    {
        [FieldOffset(0)]
        public ulong Value; // Simplified 8-byte union for layout padding
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_video_processing_feature_getset_t
    {
        public uint Size;
        public byte Version;
        public ctl_video_processing_feature_t FeatureType;
        public IntPtr ApplicationName; // char*
        public sbyte ApplicationNameLength;
        [MarshalAs(UnmanagedType.U1)]
        public bool bSet;
        public ctl_property_value_type_t ValueType;
        public ctl_property_t Value;
        public int CustomValueSize;
        public IntPtr pCustomValue; // void*
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] ReservedFields;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_video_processing_standard_color_correction_t
    {
        public uint Size;
        public byte Version;
        [MarshalAs(UnmanagedType.U1)]
        public bool standard_color_correction_enable;
        public float brightness;
        public float contrast;
        public float hue;
        public float saturation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] ReservedFields;
    }

    public static class ControlLib
    {
        private const string DLL_NAME = "ControlLib.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ctl_result_t ctlInit(
            ref ctl_init_args_t pInitDesc,
            out IntPtr phAPIHandle); // ctl_api_handle_t*

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ctl_result_t ctlClose(
            IntPtr hAPIHandle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ctl_result_t ctlEnumerateDevices(
            IntPtr hAPIHandle,
            ref uint pCount,
            [Out] IntPtr[]? phDevices); // ctl_device_adapter_handle_t*

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ctl_result_t ctlGetSetVideoProcessingFeature(
            IntPtr hDAhandle,
            ref ctl_video_processing_feature_getset_t pFeature);
    }
}
