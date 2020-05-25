// mosaic creation console app, by erin thornton

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Drawing.Drawing2D;

namespace src
{
    class Program
    {
        static int numTilesWide = 40;
        static int numTilesHigh = 40;
        static string outputFilename = "result.jpg";
        // equal energy illuminant
        static Vector3 illuminantReference = new Vector3(10, 10, 10);
        static DateTime startTime;
        
        static void Main(string[] args)
        {
            if (args == null || args.Length < 2 || args.Length > 2)
            {
                Console.WriteLine("could not interpret image file path and the folder path parameters");
                return;
            }
            Console.WriteLine("starting program");
            startTime = DateTime.Now;
            var targetImage = new Bitmap(args[0]); 
            var referenceColors = GetReferenceColors(args[1]);
            var outputPath = Directory.GetCurrentDirectory() + "\\" + outputFilename;
            var resultImage = BuildTiledImage(new Bitmap(targetImage.Width, targetImage.Height), targetImage, referenceColors);
            resultImage.Save(outputPath, ImageFormat.Jpeg);
            var now = DateTime.Now;
            Console.WriteLine("ran for " + Math.Floor((now - startTime).TotalSeconds) + " seconds, output image saved as " + outputPath);
        }

        // returns a tiled image made up of smaller reference images
        static Bitmap BuildTiledImage(Bitmap canvas, Bitmap targetImage, Dictionary<string,Color> referenceColors){
            Console.WriteLine("generating output image which is " + numTilesWide + " tiles wide and " + numTilesHigh + " tiles high");
            var tileWidth = Convert.ToInt32(Math.Floor((decimal)(canvas.Width / numTilesWide)));
            var tileHeight = Convert.ToInt32(Math.Floor((decimal)(canvas.Height / numTilesHigh)));
            for(var x = 0; x < numTilesWide; x++){
                for(var y = 0; y < numTilesHigh; y++){
                    var thisTile = GetTile(targetImage, x, y);
                    var thisTileColor = GetAverageColor(thisTile);
                    var thisTileWidth = tileWidth + (x == numTilesWide - 1 ? tileWidth + (thisTile.Width % numTilesWide) : 0);
                    var thisTileHeight = tileHeight + (y == numTilesHigh - 1 ? tileHeight + (thisTile.Height % numTilesHigh) : 0);
                    Console.WriteLine("finding image for tile x = " + (x + 1) + " | y = " + (y + 1));
                    var bestImage = new Bitmap(BestReferenceColorMatch(thisTileColor, referenceColors)); 
                    bestImage = ResizeBitmap(bestImage, thisTileWidth, thisTileHeight);
                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImage(bestImage, (x* tileWidth), (y*tileHeight), thisTileWidth, thisTileHeight);
                    }
                }
            }
            return canvas;
        }

        // resizes a bitmap to the passed in width and height
        static Bitmap ResizeBitmap(Bitmap sourceImage, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(sourceImage, 0, 0, width, height);
            }
            return result;
        }

        // returns the filepath of the best color match to the color passed in
        static string BestReferenceColorMatch(Color targetColor, Dictionary<string,Color> referenceColors){
            var result = string.Empty;
            var bestDistance = -1d;
            // break after one iteration so we have a starting value to compare to
            foreach(var referenceColor in referenceColors){
               bestDistance = GetColorDistance(targetColor, referenceColor.Value);
               result = referenceColor.Key;
               break;
            }
            foreach(var referenceColor in referenceColors){
                var thisDistance = GetColorDistance(targetColor, referenceColor.Value);
                if(thisDistance < bestDistance)
                {
                    result = referenceColor.Key;
                    bestDistance = thisDistance;
                }
            }
            if(result == string.Empty)
                Console.WriteLine("no image match found");
            else
                Console.WriteLine("best match for color (" + targetColor.R + ", " + targetColor.G + ", " + targetColor.B +") chosen: " + result);
            return result;
        }

        // gets the delta e of the two passed in colors
        static double GetColorDistance(Color targetColor, Color referenceColor){
            // CIE vector order = Lab
            var targetCIE = RGBtoCieLAB(targetColor);
            var referenceCIE = RGBtoCieLAB(referenceColor);
            double result = Math.Sqrt( ( Math.Pow( ( targetCIE.X - referenceCIE.X ), 2) )
                        + ( Math.Pow((targetCIE.Y - referenceCIE.Y ), 2 ))
                        + ( Math.Pow(( targetCIE.Z - referenceCIE.Z), 2 )) );
            return result;
        }

        // gets average colors of the reference images in the passed in folder path and adds each color as a value to a dictionary with the image path as its key
        static Dictionary<string, Color> GetReferenceColors(string folderPath){
            var result = new Dictionary<string,Color>(){};
            var images = Directory.GetFiles(folderPath.TrimEnd('\\'), "*", SearchOption.AllDirectories);
            Console.WriteLine("starting to average the colors in the " + images.Length + " image files that were detected");
            foreach(var imagePath in images){
                 var image = new Bitmap(imagePath);
                 var averageColor = GetAverageColor(image);
                 result.Add(imagePath, averageColor);
                 Console.WriteLine("average color is (" + averageColor.R + ", " + averageColor.G + ", " + averageColor.B + ") for image " + imagePath);
            }
            return result;
        }

        // gets the specified tile from the specified image, 0 indexed tile coordinates
        static Bitmap GetTile(Bitmap fullImage, int tileX, int tileY){
            var tileWidth = Convert.ToInt32(Math.Floor((decimal)(fullImage.Width / numTilesWide)));
            var tileHeight = Convert.ToInt32(Math.Floor((decimal)(fullImage.Height / numTilesHigh)));
            var thisTileWidth = tileWidth + (tileX == (numTilesWide - 1) ? (fullImage.Width % numTilesWide) : 0);
            var thisTileHeight = tileHeight + (tileY == (numTilesHigh - 1) ? (fullImage.Height % numTilesHigh) : 0);
            var cloneRect = new Rectangle(tileWidth * tileX, tileHeight * tileY, thisTileWidth, thisTileHeight);
            var thisTile = fullImage.Clone(cloneRect, fullImage.PixelFormat);
            return thisTile;
        }

        // gets the average color of a bitmap
        static Color GetAverageColor(Bitmap tile){
            long[] totalRGB = new long[3]{0,0,0};
            var totalPixels = 0;
            for(var x = 0; x < tile.Width; x++){
                for(var y = 0; y < tile.Height; y++){
                    var pixel = tile.GetPixel(x,y);
                    totalRGB[0] += Convert.ToInt32(pixel.R);
                    totalRGB[1] += Convert.ToInt32(pixel.G);
                    totalRGB[2] += Convert.ToInt32(pixel.B);
                    totalPixels++;
                }
            }
            var averagedRGB = new int[3] { Convert.ToInt32(Math.Round(((decimal)(totalRGB[0]/totalPixels)), 0)),
                                            Convert.ToInt32(Math.Round(((decimal)(totalRGB[1]/totalPixels)), 0)),
                                            Convert.ToInt32(Math.Round(((decimal)(totalRGB[2]/totalPixels)), 0))};
            return Color.FromArgb(averagedRGB[0], averagedRGB[1], averagedRGB[2]);
        }

        // converts an RGB color to a CIE color, the resulting vector holds the values L, a, and b, in that order
        static Vector3 RGBtoCieLAB(Color color){
            var rgbPercents = new Vector3((float)(color.R / 255.0), (float)(color.G / 255.0), (float)(color.B / 255.0));

            if ( rgbPercents.X > 0.04045 ) 
                rgbPercents.X = (float)Math.Pow(( ( rgbPercents.X + 0.055 ) / 1.055 ), 2.4);
            else                   
                rgbPercents.X = (float)(rgbPercents.X / 12.92);

            if ( rgbPercents.Y > 0.04045 ) 
                rgbPercents.Y = (float)Math.Pow(( ( rgbPercents.Y + 0.055 ) / 1.055 ), 2.4);
            else                   
                rgbPercents.Y = (float)(rgbPercents.Y / 12.92);
           
            if ( rgbPercents.X > 0.04045 )
                rgbPercents.X = (float)Math.Pow(( ( rgbPercents.X + 0.055 ) / 1.055 ), 2.4);
            else                   
                rgbPercents.X = (float)(rgbPercents.X / 12.92);

            rgbPercents.X = rgbPercents.X * 100;
            rgbPercents.Y = rgbPercents.Y * 100;
            rgbPercents.X = rgbPercents.X * 100;

            var xyzColor = new Vector3((float)(rgbPercents.X * 0.4124 + rgbPercents.Y * 0.3576 + rgbPercents.X * 0.1805),
                                        (float)(rgbPercents.X * 0.2126 + rgbPercents.Y * 0.7152 + rgbPercents.X * 0.0722),
                                        (float)(rgbPercents.X * 0.0193 + rgbPercents.Y * 0.1192 + rgbPercents.X * 0.9505));

            xyzColor.X = xyzColor.X / illuminantReference.X;
            xyzColor.Y = xyzColor.Y / illuminantReference.Y;
            xyzColor.Z = xyzColor.Z / illuminantReference.Z;

            if ( xyzColor.X > 0.008856 ) 
                xyzColor.X = (float)Math.Pow(xyzColor.X , ( 1/3.0 ));
            else                    
                xyzColor.X = (float)(( 7.787 * xyzColor.X ) + ( 16 / 116.0 ));
            if ( xyzColor.Y > 0.008856 ) 
                xyzColor.Y =(float)Math.Pow( xyzColor.Y , ( 1/3.0 ));
            else                   
                xyzColor.Y = (float)(( 7.787 * xyzColor.Y ) + ( 16 / 116.0 ));
            if ( xyzColor.Z > 0.008856 ) 
                xyzColor.Z = (float)Math.Pow(xyzColor.Z, ( 1/3.0 ));
            else                    
                xyzColor.Z = (float)(( 7.787 * xyzColor.Z ) + ( 16 / 116.0 ));

            var resultColor = new Vector3(( 116 * xyzColor.Y ) - 16, 
                                            500 * ( xyzColor.X - xyzColor.Y ),
                                            200 * ( xyzColor.Y - xyzColor.Z ));
            return resultColor;                           
        }
    }
}