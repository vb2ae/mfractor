﻿using System;
using System.Runtime.InteropServices;

namespace MFractor.Utilities.Clipboards
{
    /// <summary>
    /// Accesses the Osx platforms clipboard.
    /// <para/>
    /// Credit: https://github.com/SimonCropp/TextCopy
    /// </summary>
    public static class OsxClipboard
    {
        static readonly IntPtr nsString = objc_getClass("NSString");
        static readonly IntPtr nsPasteboard = objc_getClass("NSPasteboard");
        static readonly IntPtr nsStringPboardType;
        static readonly IntPtr utfTextType;
        static readonly IntPtr generalPasteboard;

        static OsxClipboard()
        {
            utfTextType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "public.utf8-plain-text");
            nsStringPboardType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "NSStringPboardType");

            generalPasteboard = objc_msgSend(nsPasteboard, sel_registerName("generalPasteboard"));
        }

        public static string GetText()
        {
            var ptr = objc_msgSend(generalPasteboard, sel_registerName("stringForType:"), nsStringPboardType);
            var charArray = objc_msgSend(ptr, sel_registerName("UTF8String"));
            return Marshal.PtrToStringAnsi(charArray);
        }

        public static void SetText(string text)
        {
            IntPtr str = default;
            try
            {
                str = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), text);
                objc_msgSend(generalPasteboard, sel_registerName("clearContents"));
                objc_msgSend(generalPasteboard, sel_registerName("setString:forType:"), str, utfTextType);
            }
            finally
            {
                if (str != default)
                {
                    objc_msgSend(str, sel_registerName("release"));
                }
            }
        }

#pragma warning disable IDE1006 // Naming Styles
        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_getClass(string className);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        static extern IntPtr sel_registerName(string selectorName);
#pragma warning restore IDE1006 // Naming Styles
    }
}
