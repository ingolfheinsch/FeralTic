﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace FeralTic.DX11.Resources
{
    public class DX11Texture2D : DX11DeviceResource<Texture2D>, IDX11ReadableResource
    {
        public Texture2DDescription Description { get { return this.desc; } }
        protected bool isowner;
        protected Texture2DDescription desc;
        protected DX11RenderContext context;

        public static DX11Texture2D FromDescription(DX11RenderContext context, Texture2DDescription desc)
        {
            DX11Texture2D res = new DX11Texture2D();
            res.context = context;
            res.Resource = new Texture2D(context.Device, desc);
            res.isowner = true;
            res.desc = desc;
            res.SRV = new ShaderResourceView(context.Device, res.Resource);

            return res;
        }

        public static DX11Texture2D FromSharedHandle(DX11RenderContext context, IntPtr sharedHandle)
        {
            Texture2D tex = context.Device.OpenSharedResource<Texture2D>(sharedHandle);
            ShaderResourceView srv = new ShaderResourceView(context.Device, tex);

            DX11Texture2D result = new DX11Texture2D();
            result.context = context;
            result.Resource = tex;
            result.SRV = srv;
            result.desc = tex.Description;

            result.isowner = true;
            return result;
        }

        /// <summary>
        /// Creates a reference resource from texture and view.
        /// This does not take ownership on the resource, so dispose will do nothing on this
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="texture">A valid Direc3D11 texture</param>
        /// <param name="shaderView">A valid Direct3D11 view (that should match the texture).</param>
        /// <returns>Packed Texture object</returns>
        public static DX11Texture2D FromTextureAndSRV(DX11RenderContext context, Texture2D texture, ShaderResourceView shaderView)
        {
            Texture2DDescription desc = texture.Description;

            DX11Texture2D res = new DX11Texture2D();
            res.context = context;
            res.Resource = texture;
            res.SRV = shaderView;
            res.desc = desc;
            res.isowner = false;
            return res;
        }

        /// <summary>
        /// Creates a reference resource from texture and view.
        /// This does take ownership over the resource. Calling dispose on the object will effectively release ressources
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="texture">A valid Direc3D11 texture</param>
        /// <param name="shaderView">A valid Direct3D11 view (that should match the texture).</param>
        /// <returns>Packed Texture object</returns>
        public static DX11Texture2D TakeOwnership(DX11RenderContext context, Texture2D texture, ShaderResourceView shaderView)
        {
            Texture2DDescription desc = texture.Description;

            DX11Texture2D res = new DX11Texture2D();
            res.context = context;
            res.Resource = texture;
            res.SRV = shaderView;
            res.desc = desc;
            res.isowner = true;
            return res;
        }

        public static Texture2D CreateStaging(DX11RenderContext context, Texture2D texture)
        {
            Texture2DDescription td = texture.Description;
            td.BindFlags = BindFlags.None;
            td.CpuAccessFlags = CpuAccessFlags.Read;
            td.Usage = ResourceUsage.Staging;

            return new Texture2D(context.Device, td);
        }

        public static DX11Texture2D CreateImmutable(DX11RenderContext context, int width, int height, SlimDX.DXGI.Format format, int pitch, IntPtr initialData)
        {
            var dataStream = new SlimDX.DataStream(initialData, pitch * height, true, false);
            return CreateImmutable(context, width, height, format, pitch, dataStream);
        }

        public static DX11Texture2D CreateImmutable(DX11RenderContext context, int width, int height, SlimDX.DXGI.Format format, int pitch, SlimDX.DataStream initialData)
        {
            Texture2DDescription desc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = format,
                Height = height,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                Width = width
            };

            SlimDX.DataRectangle dataRectangle = new SlimDX.DataRectangle(pitch, initialData);
            Texture2D texture = new Texture2D(context.Device, desc, dataRectangle);

            try
            {
                ShaderResourceView shaderView = new ShaderResourceView(context.Device, texture);
                return TakeOwnership(context, texture, shaderView);
            }
            catch
            {
                texture.Dispose(); //Avoid partial leak in case of failure, that should be really rare but could happen
                throw;
            }
        }

        public static DX11Texture2D FromResource(DX11RenderContext context, Assembly assembly, string path)
        {
            try
            {
                Stream s = assembly.GetManifestResourceStream(path);
                Texture2D tex = Texture2D.FromStream(context.Device, s,(int)s.Length);

                if (tex.Description.ArraySize == 1)
                {
                    DX11Texture2D res = new DX11Texture2D();
                    res.context = context;
                    res.Resource = tex;
                    res.SRV = new ShaderResourceView(context.Device, res.Resource);
                    res.desc = res.Resource.Description;
                    res.isowner = true;
                    return res;
                }
                else
                {
                    if (tex.Description.OptionFlags.HasFlag(ResourceOptionFlags.TextureCube))
                    {
                        return new DX11TextureCube(context, tex);
                    }
                    else
                    {
                        return new DX11TextureArray2D(context, tex);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public static DX11Texture2D FromMemory(DX11RenderContext context, byte[] data)
        {
            return FromMemory(context, data, ImageLoadInformation.FromDefaults());
        }

        public static DX11Texture2D FromMemory(DX11RenderContext context, byte[] data, ImageLoadInformation loadinfo)
        {
            try
            {
                Texture2D tex = Texture2D.FromMemory(context.Device, data, loadinfo);

                if (tex.Description.ArraySize == 1)
                {
                    DX11Texture2D res = new DX11Texture2D();
                    res.context = context;
                    res.Resource = tex;
                    res.SRV = new ShaderResourceView(context.Device, res.Resource);
                    res.desc = res.Resource.Description;
                    res.isowner = true;
                    return res;
                }
                else
                {
                    if (tex.Description.OptionFlags.HasFlag(ResourceOptionFlags.TextureCube))
                    {
                        return new DX11TextureCube(context, tex);
                    }
                    else
                    {
                        return new DX11TextureArray2D(context, tex);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public static DX11Texture2D FromStream(DX11RenderContext context, Stream s, int size)
        {
            try
            {
                Texture2D tex = Texture2D.FromStream(context.Device, s, size);

                if (tex.Description.ArraySize == 1)
                {
                    DX11Texture2D res = new DX11Texture2D();
                    res.context = context;
                    res.Resource = tex;
                    res.SRV = new ShaderResourceView(context.Device, res.Resource);
                    res.desc = res.Resource.Description;
                    res.isowner = true;
                    return res;
                }
                else
                {
                    if (tex.Description.OptionFlags.HasFlag(ResourceOptionFlags.TextureCube))
                    {
                        return new DX11TextureCube(context, tex);
                    }
                    else
                    {
                        return new DX11TextureArray2D(context, tex);
                    }
                }
            }
            catch
            {
                throw;
            }
        }


        public static DX11Texture2D FromFile(DX11RenderContext context, string path)
        {
            return DX11Texture2D.FromFile(context, path, ImageLoadInformation.FromDefaults());
        }

        public static DX11Texture2D FromFile(DX11RenderContext context, string path, ImageLoadInformation loadinfo)
        {
            try
            {
                Texture2D tex = Texture2D.FromFile(context.Device, path, loadinfo);

                if (tex.Description.ArraySize == 1)
                {
                    DX11Texture2D res = new DX11Texture2D();
                    res.context = context;
                    res.Resource = tex;
                    res.SRV = new ShaderResourceView(context.Device, res.Resource);
                    res.desc = res.Resource.Description;
                    res.isowner = true;
                    return res;
                }
                else
                {
                    if (tex.Description.OptionFlags.HasFlag(ResourceOptionFlags.TextureCube))
                    {
                        return new DX11TextureCube(context, tex);
                    }
                    else
                    {
                        return new DX11TextureArray2D(context, tex);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public Format Format { get { return this.desc.Format; } }
        public int Width { get { return this.desc.Width; } }
        public int Height { get { return this.desc.Height; } }



        public override void Dispose()
        {
            if (isowner)
            {
                if (this.SRV != null)
                {
                    this.SRV.Dispose();
                    this.SRV = null;
                }
               

                this.Resource.Dispose();
            }
        }
    }
}
