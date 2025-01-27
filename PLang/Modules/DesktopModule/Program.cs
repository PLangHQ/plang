using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using System.Collections;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Runtime.InteropServices;
using System.Text;

namespace PLang.Modules.DesktopModule
{
	[Description("Get information about what windows are open on the operating system")]
	public class Program : BaseProgram
	{
		

		public class WindowInfo
		{
			public string Title { get; set; }
			public string ClassName { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
			public int X { get; set; }
			public int Y { get; set; }
		}
		public async Task<List<WindowInfo>> GetOpenWindows()
		{

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return GetWindowsOnWindows();
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return GetWindowsOnLinux();
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return GetWindowsOnMacOs();
			}


			return new();
		}

		private static List<WindowInfo> GetWindowsOnWindows()
		{
			List<WindowInfo> windowList = new List<WindowInfo>();
			EnumWindows((hWnd, lParam) =>
			{
				if (IsWindowVisible(hWnd))
				{
					int titleLength = GetWindowTextLength(hWnd);
					if (titleLength > 0)
					{
						StringBuilder title = new StringBuilder(titleLength + 1);
						GetWindowText(hWnd, title, title.Capacity);

						RECT rect;
						if (GetWindowRect(hWnd, out rect))
						{
							StringBuilder className = new StringBuilder(256);
							GetClassName(hWnd, className, className.Capacity);

							windowList.Add(new WindowInfo
							{
								Title = title.ToString(),
								ClassName = className.ToString(),
								Width = rect.Width,
								Height = rect.Height,
								X = rect.Left,
								Y = rect.Top,
							});
						}
					}
				}
				return true;
			}, IntPtr.Zero);
			return windowList;
		}


		/* Windows */
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;

			public int Width => Right - Left;
			public int Height => Bottom - Top;
		}


		/* Linux */
		[DllImport("libX11.so.6")]
		private static extern IntPtr XOpenDisplay(IntPtr display);

		[DllImport("libX11.so.6")]
		private static extern int XCloseDisplay(IntPtr display);

		[DllImport("libX11.so.6")]
		private static extern IntPtr XDefaultRootWindow(IntPtr display);

		[DllImport("libX11.so.6")]
		private static extern bool XQueryTree(IntPtr display, IntPtr window, out IntPtr rootReturn, out IntPtr parentReturn, out IntPtr[] childrenReturn, out int nChildren);

		[DllImport("libX11.so.6")]
		private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr windowName);

		[DllImport("libX11.so.6")]
		private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

		[DllImport("libX11.so.6")]
		private static extern int XGetClassHint(IntPtr display, IntPtr window, out XClassHint classHint);

		[DllImport("libX11.so.6")]
		private static extern int XFree(IntPtr data);

		[StructLayout(LayoutKind.Sequential)]
		public struct XWindowAttributes
		{
			public int X;
			public int Y;
			public int Width;
			public int Height;
			public int BorderWidth;
			public int Depth;
			// Other fields are omitted for simplicity
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XClassHint
		{
			public IntPtr ResName;
			public IntPtr ResClass;
		}


		private static List<WindowInfo> GetWindowsOnLinux()
		{
			List<WindowInfo> windowList = new List<WindowInfo>();

			IntPtr display = XOpenDisplay(IntPtr.Zero);
			if (display == IntPtr.Zero)
			{
				Console.WriteLine("Unable to open display.");
				return windowList;
			}

			IntPtr root = XDefaultRootWindow(display);

			if (XQueryTree(display, root, out _, out _, out IntPtr[] children, out int nChildren))
			{
				foreach (var child in children)
				{
					if (XFetchName(display, child, out IntPtr windowName) != 0 && windowName != IntPtr.Zero)
					{
						string title = Marshal.PtrToStringAnsi(windowName);
						XFree(windowName);

						if (!string.IsNullOrWhiteSpace(title))
						{
							// Get window attributes
							if (XGetWindowAttributes(display, child, out XWindowAttributes attributes) != 0)
							{
								// Get class name
								string className = GetWindowClassName(display, child);

								windowList.Add(new WindowInfo
								{
									Title = title,
									ClassName = className,
									X = attributes.X,
									Y = attributes.Y,
									Width = attributes.Width,
									Height = attributes.Height
								});
							}
						}
					}
				}
			}

			XCloseDisplay(display);
			return windowList;
		}

		private static string GetWindowClassName(IntPtr display, IntPtr window)
		{
			if (XGetClassHint(display, window, out XClassHint classHint) != 0)
			{
				string className = Marshal.PtrToStringAnsi(classHint.ResClass);
				XFree(classHint.ResName);
				XFree(classHint.ResClass);
				return className;
			}
			return string.Empty;
		}


		/* Macos */


		[StructLayout(LayoutKind.Sequential)]
		public struct CGPoint
		{
			public double X;
			public double Y;

			public override string ToString()
			{
				return $"X={X}, Y={Y}";
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CGSize
		{
			public double Width;
			public double Height;

			public override string ToString()
			{
				return $"Width={Width}, Height={Height}";
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CGRect
		{
			public CGPoint Origin;
			public CGSize Size;

			public override string ToString()
			{
				return $"Position={Origin}, Size={Size}";
			}
		}

		public class WindowInfo2	
		{
			public string Title { get; set; }
			public double Width { get; set; }
			public double Height { get; set; }
			public CGPoint Position { get; set; }
		}

		[DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
		private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern void CFRelease(IntPtr cf);

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string str, uint encoding);

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern int CFArrayGetCount(IntPtr array);

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, int index);



		private static List<WindowInfo> GetWindowsOnMacOs()
		{
			const uint kCGWindowListOptionAll = 0;
			const uint kCGNullWindowID = 0;

			var windowList = new List<WindowInfo>();
			IntPtr windowInfoList = CGWindowListCopyWindowInfo(kCGWindowListOptionAll, kCGNullWindowID);

			if (windowInfoList == IntPtr.Zero)
			{
				Console.WriteLine("Unable to retrieve window list.");
				return windowList;
			}

			int count = CFArrayGetCount(windowInfoList);
			for (int i = 0; i < count; i++)
			{
				IntPtr windowInfo = CFArrayGetValueAtIndex(windowInfoList, i);

				if (windowInfo != IntPtr.Zero)
				{
					string title = GetCFDictionaryStringValue(windowInfo, "kCGWindowName");
					CGRect frame = GetCFDictionaryCGRectValue(windowInfo, "kCGWindowBounds");

					windowList.Add(new WindowInfo
					{
						Title = title ?? "Untitled",
						Width = (int)frame.Size.Width,
						Height = (int)frame.Size.Height,
						ClassName = title,
						X = (int) frame.Origin.X,
						Y = (int) frame.Origin.Y

					});
				}
			}

			CFRelease(windowInfoList);
			return windowList;
		}

		private static string GetCFDictionaryStringValue(IntPtr dictionary, string key)
		{
			IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, 0x08000100); // UTF-8 Encoding
			IntPtr cfValue = CFDictionaryGetValue(dictionary, cfKey);
			string value = cfValue != IntPtr.Zero ? Marshal.PtrToStringAnsi(CFStringGetCStringPtr(cfValue, 0x08000100)) : null;
			CFRelease(cfKey);
			return value;
		}

		private static CGRect GetCFDictionaryCGRectValue(IntPtr dictionary, string key)
		{
			IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, 0x08000100); // UTF-8 Encoding
			IntPtr cfValue = CFDictionaryGetValue(dictionary, cfKey);

			CGRect rect = new CGRect();
			if (cfValue != IntPtr.Zero)
			{
				rect = (CGRect)Marshal.PtrToStructure(cfValue, typeof(CGRect));
			}

			CFRelease(cfKey);
			return rect;
		}

		[DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
		private static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

	}
}
