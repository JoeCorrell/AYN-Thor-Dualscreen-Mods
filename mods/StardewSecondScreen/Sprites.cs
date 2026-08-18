using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace StardewSecondScreen
{

    internal sealed class Sprites
    {
        private readonly HashSet<string> _sent = new();

        public void Reset() => _sent.Clear();

        public bool AlreadySent(string id) => _sent.Contains(id);

        public byte[]? EncodeRegion(Texture2D? sheet, Microsoft.Xna.Framework.Rectangle source)
        {
            try
            {
                if (sheet == null || source.Width <= 0 || source.Height <= 0) return null;

                if (source.Right > sheet.Width || source.Bottom > sheet.Height) return null;

                var pixels = new Microsoft.Xna.Framework.Color[source.Width * source.Height];
                sheet.GetData(0, source, pixels, 0, pixels.Length);

                using var cut = new Texture2D(sheet.GraphicsDevice, source.Width, source.Height);
                cut.SetData(pixels);

                using var stream = new MemoryStream();
                cut.SaveAsPng(stream, source.Width, source.Height);
                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public byte[]? EncodeId(string qualifiedId)
        {
            try
            {
                if (string.IsNullOrEmpty(qualifiedId)) return null;
                ParsedItemData? data = ItemRegistry.GetData(qualifiedId);
                if (data == null) return null;
                var png = Cut(data);
                if (png != null) _sent.Add(qualifiedId);
                return png;
            }
            catch
            {
                return null;
            }
        }

        private byte[]? Cut(ParsedItemData data)
        {
            Texture2D sheet = data.GetTexture();
            var source = data.GetSourceRect();
            if (sheet == null || source.Width <= 0 || source.Height <= 0) return null;
            if (source.Right > sheet.Width || source.Bottom > sheet.Height) return null;

            var pixels = new Microsoft.Xna.Framework.Color[source.Width * source.Height];
            sheet.GetData(0, source, pixels, 0, pixels.Length);

            using var cut = new Texture2D(sheet.GraphicsDevice, source.Width, source.Height);
            cut.SetData(pixels);

            using var stream = new MemoryStream();
            cut.SaveAsPng(stream, source.Width, source.Height);
            return stream.ToArray();
        }

        public byte[]? Encode(Item item)
        {
            try
            {
                ParsedItemData data = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
                Texture2D sheet = data.GetTexture();
                var source = data.GetSourceRect();
                if (sheet == null || source.Width <= 0 || source.Height <= 0) return null;

                var pixels = new Microsoft.Xna.Framework.Color[source.Width * source.Height];
                sheet.GetData(0, source, pixels, 0, pixels.Length);

                using var cut = new Texture2D(sheet.GraphicsDevice, source.Width, source.Height);
                cut.SetData(pixels);

                using var stream = new MemoryStream();
                cut.SaveAsPng(stream, source.Width, source.Height);
                _sent.Add(item.QualifiedItemId);
                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
