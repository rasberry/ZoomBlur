//#define MEMOIZE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZoomBlur
{
	class Program
	{
		static void Main(string[] args)
		{
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;
			#if DEBUG
			Trace.Listeners.Add(new ConsoleTraceListener());
			#endif

			if (!ProcessArgs(args) || !ValidateArgs()) {
				return;
			}

			var inpmap = Bitmap.FromFile(InputImage) as Bitmap;
			var outmap = new Bitmap(inpmap.Width,inpmap.Height,PixelFormat.Format32bppArgb);

			using (InputBitmap = new LockBitmap(inpmap))
			using (OutputBitmap = new LockBitmap(outmap))
			{
				InputBitmap.LockBits();
				OutputBitmap.LockBits();
				RenderZoom(inpmap);
			}

			outmap.Save(OutputImage);
		}

		static void RenderZoom(Bitmap inpmap)
		{
			//TODO make parallel [see https://github.com/rasberry/AreaSmoother/blob/master/Program.cs]
			//TODO do timing test vs imagemagick
			double cx = inpmap.Width / 2.0;
			double cy = inpmap.Height / 2.0;
			double mult = ZoomAmount;
			double maxdist = Math.Sqrt(cy * cy + cx * cx);

			for (double y = 0; y < inpmap.Height; y++)
			{
				Console.WriteLine("y = " + y);
				for (double x = 0; x < inpmap.Width; x++)
				{
					double dist = Math.Sqrt((y - cy) * (y - cy) + (x - cx) * (x - cx));
					int idist = (int)Math.Ceiling(dist);

					List<Color> vector = new List<Color>(idist);
					double ang = Math.Atan2(y - cy, x - cx);

					//double sd = dist/mult;
					//double ed = dist/mult + mult;
					//double sd = 0.1, ed = dist/mult;
					//double scale = maxdist/dist;
					//double ed = mult * scale;

					double ed = dist;
					double sd = dist * mult;

					for (double d = sd; d < ed; d++)
					{
						double px = Math.Cos(ang) * d + cx;
						double py = Math.Sin(ang) * d + cy;
						int ipx = (int)Math.Round(px, 0);
						int ipy = (int)Math.Round(py, 0);
						Color c = GetAliasedColor(InputBitmap, ipx, ipy);
						vector.Add(c);
					}

					Color avg;
					int count = vector.Count;
					if (count == 0)
					{
						avg = InputBitmap.GetPixel((int)x, (int)y);
					}
					else
					{
						int cr = 0, cg = 0, cb = 0, ca = 0;
						foreach (Color c in vector)
						{
							cr += c.R; cg += c.G; cb += c.B;
							ca += c.A;
						}
						avg = Color.FromArgb(ca / count, cr / count, cg / count, cb / count);
					}
					OutputBitmap.SetPixel((int)x, (int)y, avg);
				}
			}
		}

		static Color GetAliasedColor(LockBitmap lb, int x, int y)
		{
			Color
				 c00 = GetExtendedPixel(lb,x-1,y-1)
				,c01 = GetExtendedPixel(lb,x+0,y-1)
				,c02 = GetExtendedPixel(lb,x+1,y-1)
				,c10 = GetExtendedPixel(lb,x-1,y+0)
				,c11 = GetExtendedPixel(lb,x+0,y+0)
				,c12 = GetExtendedPixel(lb,x+1,y+0)
				,c20 = GetExtendedPixel(lb,x-1,y+1)
				,c21 = GetExtendedPixel(lb,x+0,y+1)
				,c22 = GetExtendedPixel(lb,x+1,y+1)
			;
			
			#if !MEMOIZE
			double d1 = 1.0/16.0;
			double d2 = 2.0/16.0;
			double d4 = 4.0/16.0;

			double a =
				  d1 * c00.A + d2 * c01.A + d1 * c02.A
				+ d2 * c10.A + d4 * c11.A + d2 * c12.A
				+ d1 * c20.A + d2 * c21.A + d1 * c22.A
			;
			double r =
				  d1 * c00.R + d2 * c01.R + d1 * c02.R
				+ d2 * c10.R + d4 * c11.R + d2 * c12.R
				+ d1 * c20.R + d2 * c21.R + d1 * c22.R
			;
			double g =
				  d1 * c00.G + d2 * c01.G + d1 * c02.G
				+ d2 * c10.G + d4 * c11.G + d2 * c12.G
				+ d1 * c20.G + d2 * c21.G + d1 * c22.G
			;
			double b =
				  d1 * c00.B + d2 * c01.B + d1 * c02.B
				+ d2 * c10.B + d4 * c11.B + d2 * c12.B
				+ d1 * c20.B + d2 * c21.B + d1 * c22.B
			;

			#else
			double a =
				  AccessMemoize(1,c00.A) + AccessMemoize(2,c01.A) + AccessMemoize(1,c02.A)
				+ AccessMemoize(2,c10.A) + AccessMemoize(4,c11.A) + AccessMemoize(2,c12.A)
				+ AccessMemoize(1,c20.A) + AccessMemoize(2,c21.A) + AccessMemoize(1,c22.A)
			;
			double r =
				  AccessMemoize(1,c00.R) + AccessMemoize(2,c01.R) + AccessMemoize(1,c02.R)
				+ AccessMemoize(2,c10.R) + AccessMemoize(4,c11.R) + AccessMemoize(2,c12.R)
				+ AccessMemoize(1,c20.R) + AccessMemoize(2,c21.R) + AccessMemoize(1,c22.R)
			;
			double g =
				  AccessMemoize(1,c00.G) + AccessMemoize(2,c01.G) + AccessMemoize(1,c02.G)
				+ AccessMemoize(2,c10.G) + AccessMemoize(4,c11.G) + AccessMemoize(2,c12.G)
				+ AccessMemoize(1,c20.G) + AccessMemoize(2,c21.G) + AccessMemoize(1,c22.G)
			;
			double b =
				  AccessMemoize(1,c00.B) + AccessMemoize(2,c01.B) + AccessMemoize(1,c02.B)
				+ AccessMemoize(2,c10.B) + AccessMemoize(4,c11.B) + AccessMemoize(2,c12.B)
				+ AccessMemoize(1,c20.B) + AccessMemoize(2,c21.B) + AccessMemoize(1,c22.B)
			;
			#endif
			return Color.FromArgb((int)a,(int)r,(int)g,(int)b);
		}

		static Color GetExtendedPixel(LockBitmap b,int x, int y)
		{
			int w = b.Width -1;
			int h = b.Height -1;
			int bx,by;

			if (y < 0) { by = 0; }
			else if (y > h) { by = h; }
			else { by = y; }

			if (x < 0) { bx = 0; }
			else if (x > w) { bx = h; }
			else { bx = x; }

			return b.GetPixel(bx,by);
		}
		
		#if MEMOIZE
		static bool[,] hasmemo = new bool[3,256];
		static double[,] memo = new double[3,256];
		static double AccessMemoize(int which, byte num)
		{
			//1,2,4
			int index = which - 1;
			if (index == 3) { index = 2; }
			//Console.WriteLine("get "+which+" "+num+" ["+index+"]");
			if (hasmemo[index,num])
			{
				return memo[index,num];
			}
			else
			{
				double answer = which/16.0 * num;
				Console.WriteLine("adding "+which+" "+num+" "+answer);
				hasmemo[index,num] = true;
				memo[index,num] = answer;
				return answer;
			}
		}
		#endif

		static string InputImage = null;
		static string OutputImage = null;
		static double ZoomAmount = double.NaN;
		static LockBitmap InputBitmap = null;
		static LockBitmap OutputBitmap = null;

		static bool ProcessArgs(string[] args)
		{
			for(int a=0; a<args.Length; a++)
			{
				bool hasNext = a < args.Length - 1;
				if (args[a] == "-z") {
					if (!hasNext) {
						Console.WriteLine("Error: no parameter given for -z");
						return false;
					}
					string sval = args[++a];
					if (!double.TryParse(sval, out ZoomAmount)) {
						Console.WriteLine("Error: could not parse "+sval+" as a number");
						return false;
					}
				}
				else if (InputImage == null) {
					InputImage = args[a];
				}
				else if (OutputImage == null) {
					OutputImage = args[a];
				}
			}

			return true;
		}

		static bool ValidateArgs()
		{
			if (InputImage == null) {
				Console.WriteLine("no input image given");
				return false;
			} else if (OutputImage == null) {
				Console.WriteLine("no output image given");
				return false;
			} else if (!File.Exists(InputImage)) {
				Console.WriteLine("input image does not exist");
				return false;
			} else if (File.Exists(OutputImage)) {
				Console.WriteLine("output image already exist");
				return false;
			} else if (ZoomAmount < 0.0 || double.IsInfinity(ZoomAmount) || double.IsNaN(ZoomAmount)) {
				Console.Write("zoom value "+ZoomAmount+" is invalid");
				return false;
			}
			return true;
		}
	}
}
