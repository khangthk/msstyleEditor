﻿using libmsstyle;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msstyleEditor
{
    class PartRenderer
    {
        private VisualStyle m_style;
        private StylePart m_part;

        public PartRenderer(VisualStyle style, StylePart part)
        {
            m_style = style;
            m_part = part;
        }

        public Bitmap RenderPreview()
        {
            Bitmap surface = new Bitmap(200, 150, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(surface))
            {
                DrawBackground(g);
            }

            return surface;
        }

        private void DrawBackground(Graphics g)
        {
            int bgType = 2;
            if(!m_part.States[0].TryGetPropertyValue(IDENTIFIER.BGTYPE, ref bgType))
            {
                return;
            }

            if(bgType == 1 /* BORDERFILL */)
            {
                Color bgFill = Color.White;
                if (!m_part.States[0].TryGetPropertyValue(IDENTIFIER.FILLCOLOR, ref bgFill))
                {
                    return;
                }

                g.FillRectangle(new SolidBrush(bgFill), g.ClipBounds);
            }
            else if(bgType == 0 /* IMAGEFILL */)
            {
                var imageFileProp = m_part.States[0].Properties.Find((p) => p.Header.nameID == (int)IDENTIFIER.IMAGEFILE);
                if (imageFileProp == default(StyleProperty))
                {
                    imageFileProp = m_part.States[0].Properties.Find((p) => p.Header.nameID == (int)IDENTIFIER.IMAGEFILE1);
                    if (imageFileProp == default(StyleProperty))
                    {
                        return;
                    }
                }

                Image fullImage = null;

                string file = m_style.GetQueuedResourceUpdate(imageFileProp.Header.shortFlag, StyleResourceType.Image);
                if (!String.IsNullOrEmpty(file))
                {
                    fullImage = Image.FromFile(file);
                }
                else
                {
                    var resource = m_style.GetResourceFromProperty(imageFileProp);
                    if (resource?.Data != null)
                    {
                        fullImage = Image.FromStream(new MemoryStream(resource.Data));
                    }
                    else
                    {
                        return;
                    }
                }

                Rectangle imagePartToDraw = new Rectangle(0, 0, fullImage.Width, fullImage.Height);

                int imageLayout = 0; // VERTICAL
                bool haveImageLayout = m_part.States[0].TryGetPropertyValue(IDENTIFIER.IMAGELAYOUT, ref imageLayout);

                int imageCount = 1;
                bool haveImageCount = m_part.States[0].TryGetPropertyValue(IDENTIFIER.IMAGECOUNT, ref imageCount);

                if (imageCount > 1)
                {
                    int partW = imageLayout == 1
                        ? fullImage.Width / imageCount
                        : fullImage.Width;
                    int partH = imageLayout == 0
                        ? fullImage.Height / imageCount
                        : fullImage.Height;
                    int n = 0;

                    imagePartToDraw = new Rectangle(
                        0 + (partW * n),
                        0 + (partH * n),
                        partW, partH
                    );
                }


                int sizingType = 0; // TRUESIZE
                bool haveSizingTypes = m_part.States[0].TryGetPropertyValue(IDENTIFIER.SIZINGTYPE, ref sizingType);

                Margins sizingMargins = default(Margins);
                bool haveSizingMargins = m_part.States[0].TryGetPropertyValue(IDENTIFIER.SIZINGMARGINS, ref sizingMargins);


                if (sizingType == 0) // TRUESIZE
                {
                    Rectangle dst = new Rectangle(0, 0, imagePartToDraw.Width, imagePartToDraw.Height);
                    g.DrawImage(fullImage, dst, imagePartToDraw, GraphicsUnit.Pixel);
                }
                else if (sizingType == 1 || // STRETCH
                         sizingType == 2)   // TILE
                {
                    if (haveSizingMargins) // 9-slice-scaling
                    {
                        // Margin contains distance from the sides.
                        // Rectangle contains absolute values.
                        Rectangle absMargins = new Rectangle(
                            sizingMargins.Left,
                            sizingMargins.Top,
                            Math.Max(imagePartToDraw.Width - sizingMargins.Left - sizingMargins.Right, 1), // some margins are invalid.
                            Math.Max(imagePartToDraw.Height - sizingMargins.Top - sizingMargins.Bottom, 1) // some margins are invalid.
                        );

                        DrawImage9SliceScaled(g, fullImage, imagePartToDraw, Rectangle.Round(g.VisibleClipBounds), absMargins);
                    }
                    else // normal scaling
                    {
                        g.DrawImage(fullImage, g.VisibleClipBounds, imagePartToDraw, GraphicsUnit.Pixel); 
                    }
                }
                else return; // unsupported
            }
            else
            {
                return; /* NONE */
            }
        }

        private void DrawImage9SliceScaled(Graphics g, Image image, Rectangle src, Rectangle dst, Rectangle sm)
        {
            /* SRC */
            /* Assumes that sm is adjusted to this image */
            Rectangle topLeft   = new Rectangle(src.X           , src.Y, sm.Left                , sm.Top);
            Rectangle topMiddle = new Rectangle(src.X + sm.Left , src.Y, sm.Width               , sm.Top);
            Rectangle topRight  = new Rectangle(src.X + sm.Right, src.Y, src.Width - sm.Right   , sm.Top);

            Rectangle midLeft   = new Rectangle(src.X           , src.Y + sm.Top, sm.Left                   , sm.Height);
            Rectangle midMiddle = new Rectangle(src.X + sm.Left , src.Y + sm.Top, sm.Width                  , sm.Height);
            Rectangle midRight  = new Rectangle(src.X + sm.Right, src.Y + sm.Top, src.Width - sm.Right      , sm.Height);

            Rectangle botLeft   = new Rectangle(src.X           , src.Y + sm.Bottom, sm.Left                    , src.Height - sm.Bottom);
            Rectangle botMiddle = new Rectangle(src.X + sm.Left , src.Y + sm.Bottom, sm.Width                   , src.Height - sm.Bottom);
            Rectangle botRight  = new Rectangle(src.X + sm.Right, src.Y + sm.Bottom, src.Width - sm.Right       , src.Height - sm.Bottom);

            /* DST */
            int varWidth = dst.Width - sm.Left - (src.Width - sm.Right);
            int varHeight = dst.Height - sm.Top - (src.Height - sm.Bottom);

            Rectangle topLeftDst    = new Rectangle(dst.X                  , dst.Y, topLeft.Width , topLeft.Height);      // keep W & H
            Rectangle topMiddleDst  = new Rectangle(dst.X + sm.Left        , dst.Y, varWidth      , topMiddle.Height);    // keep H
            Rectangle topRightDst   = new Rectangle(dst.X + sm.Left + varWidth, dst.Y, topRight.Width, topRight.Height);     // keep W & H

            Rectangle midLeftDst    = new Rectangle(dst.X                  , dst.Y + sm.Top, midLeft.Width, varHeight); // keep W
            Rectangle midMiddleDst  = new Rectangle(dst.X + sm.Left        , dst.Y + sm.Top, varWidth     , varHeight); // fully scale
            Rectangle midRightDst   = new Rectangle(dst.X + sm.Left + varWidth, dst.Y + sm.Top, midRight.Width, varHeight); // keep W

            Rectangle botLeftDst    = new Rectangle(dst.X                  , dst.Y + sm.Top + varHeight, botLeft.Width    , botLeft.Height);      // keep W & H
            Rectangle botMiddleDst  = new Rectangle(dst.X + sm.Left        , dst.Y + sm.Top + varHeight, varWidth         , botMiddle.Height);    // keep H
            Rectangle botRightDst   = new Rectangle(dst.X + sm.Left + varWidth, dst.Y + sm.Top + varHeight, botRight.Width   , botRight.Height);     // keep W & H

            g.DrawImage(image, topLeftDst, topLeft, GraphicsUnit.Pixel);
            g.DrawImage(image, topMiddleDst, topMiddle, GraphicsUnit.Pixel);
            g.DrawImage(image, topRightDst, topRight, GraphicsUnit.Pixel);

            g.DrawImage(image, midLeftDst, midLeft, GraphicsUnit.Pixel);
            g.DrawImage(image, midMiddleDst, midMiddle, GraphicsUnit.Pixel);
            g.DrawImage(image, midRightDst, midRight, GraphicsUnit.Pixel);

            g.DrawImage(image, botLeftDst, botLeft, GraphicsUnit.Pixel);
            g.DrawImage(image, botMiddleDst, botMiddle, GraphicsUnit.Pixel);
            g.DrawImage(image, botRightDst, botRight, GraphicsUnit.Pixel);
        }
    }
}
