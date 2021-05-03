/******************************************************************************
 * Spine Runtimes Software License
 * Version 2.3
 * 
 * Copyright (c) 2013-2015, Esoteric Software
 * All rights reserved.
 * 
 * You are granted a perpetual, non-exclusive, non-sublicensable and
 * non-transferable license to use, install, execute and perform the Spine
 * Runtimes Software (the "Software") and derivative works solely for personal
 * or internal use. Without the written permission of Esoteric Software (see
 * Section 2 of the Spine Software License Agreement), you may not (a) modify,
 * translate, adapt or otherwise create derivative works, improvements of the
 * Software or develop new applications using the Software or (b) remove,
 * delete, alter or obscure any trademarks or any copyright, trademark, patent
 * or other intellectual property or proprietary rights notices on or in the
 * Software, including any copy thereof. Redistributions in binary or source
 * form must include this license and terms.
 * 
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Spine {
	/// <summary>Draws region and mesh attachments.</summary>
	public class SkeletonRenderer {
		private const int TL = 0;
		private const int TR = 1;
		private const int BL = 2;
		private const int BR = 3;

		SkeletonClipping clipper = new SkeletonClipping();	
		GraphicsDevice device;
		MeshBatcher batcher;
		public MeshBatcher Batcher { get { return batcher; } }
		RasterizerState rasterizerState;
		float[] vertices = new float[8];
		int[] quadTriangles = { 0, 1, 2, 2, 3, 0 };
		BlendState defaultBlendState;

		Effect effect;

		public bool PremultipliedAlpha { get; set; }

		public SkeletonRenderer (GraphicsDevice device) {
			this.device = device;

			batcher = new MeshBatcher();

            effect = new BasicEffect(device)
            {
                World = Matrix.Identity,
                View = Matrix.CreateLookAt(new Vector3(0.0f, 0.0f, 1.0f), Vector3.Zero, Vector3.Up),
                TextureEnabled = true,
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 1, 0)
            };

			rasterizerState = new RasterizerState();
			rasterizerState.CullMode = CullMode.None;

			Bone.yDown = true;
		}

		public void Begin () {
			defaultBlendState = PremultipliedAlpha ? BlendState.AlphaBlend : BlendState.NonPremultiplied;

			device.RasterizerState = rasterizerState;
			device.BlendState = defaultBlendState;			
		}

		public void End ()
		{
			foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
				pass.Apply();
				batcher.Draw(device);
			}
		}

		public void Draw(Skeleton skeleton) {
			var drawOrder = skeleton.DrawOrder;
			var drawOrderItems = skeleton.DrawOrder.Items;
			float skeletonR = skeleton.R, skeletonG = skeleton.G, skeletonB = skeleton.B, skeletonA = skeleton.A;
			Color color = new Color();

			for (int i = 0, n = drawOrder.Count; i < n; i++) {
				Slot slot = drawOrderItems[i];
				Attachment attachment = slot.Attachment;

				float attachmentColorR, attachmentColorG, attachmentColorB, attachmentColorA;
				Texture2D texture = null;
				int verticesCount = 0;
				float[] vertices = this.vertices;
				int indicesCount = 0;
				int[] indices = null;
				float[] uvs = null;

				if (attachment is RegionAttachment) {
					RegionAttachment regionAttachment = (RegionAttachment)attachment;
					attachmentColorR = regionAttachment.R; attachmentColorG = regionAttachment.G; attachmentColorB = regionAttachment.B; attachmentColorA = regionAttachment.A;
					AtlasRegion region = (AtlasRegion)regionAttachment.RendererObject;
					texture = (Texture2D)region.page.rendererObject;
					verticesCount = 4;
					regionAttachment.ComputeWorldVertices(slot.Bone, vertices, 0, 2);
					indicesCount = 6;
					indices = quadTriangles;
					uvs = regionAttachment.UVs;
				}
				else if (attachment is MeshAttachment) {
					MeshAttachment mesh = (MeshAttachment)attachment;
					attachmentColorR = mesh.R; attachmentColorG = mesh.G; attachmentColorB = mesh.B; attachmentColorA = mesh.A;
					AtlasRegion region = (AtlasRegion)mesh.RendererObject;
					texture = (Texture2D)region.page.rendererObject;
					int vertexCount = mesh.WorldVerticesLength;
					if (vertices.Length < vertexCount) vertices = new float[vertexCount];
					verticesCount = vertexCount >> 1;
					mesh.ComputeWorldVertices(slot, vertices);
					indicesCount = mesh.Triangles.Length;
					indices = mesh.Triangles;
					uvs = mesh.UVs;
				}
				else if (attachment is ClippingAttachment) {
					ClippingAttachment clip = (ClippingAttachment)attachment;
					clipper.ClipStart(slot, clip);
					continue;
				}
				else {
					continue;
				}

				// set blend state
				BlendState blend = slot.Data.BlendMode == BlendMode.Additive ? BlendState.Additive : defaultBlendState;
				if (device.BlendState != blend) {
					//End();
					//device.BlendState = blend;
				}

				// calculate color
				float a = skeletonA * slot.A * attachmentColorA;
				if (PremultipliedAlpha) {
					color = new Color(
							skeletonR * slot.R * attachmentColorR * a,
							skeletonG * slot.G * attachmentColorG * a,
							skeletonB * slot.B * attachmentColorB * a, a);
				}
				else {
					color = new Color(
							skeletonR * slot.R * attachmentColorR,
							skeletonG * slot.G * attachmentColorG,
							skeletonB * slot.B * attachmentColorB, a);
				}

				Color darkColor = new Color();
				if (slot.HasSecondColor) {
					if (PremultipliedAlpha) {
						darkColor = new Color(slot.R2 * a, slot.G2 * a, slot.B2 * a);
					} else {
						darkColor = new Color(slot.R2 * a, slot.G2 * a, slot.B2 * a);
					}
				}
				darkColor.A = PremultipliedAlpha ? (byte)255 : (byte)0;

				// clip
				if (clipper.IsClipping) {
					clipper.ClipTriangles(vertices, verticesCount << 1, indices, indicesCount, uvs);
					vertices = clipper.ClippedVertices.Items;
					verticesCount = clipper.ClippedVertices.Count >> 1;
					indices = clipper.ClippedTriangles.Items;
					indicesCount = clipper.ClippedTriangles.Count;
					uvs = clipper.ClippedUVs.Items;
				}

				if (verticesCount == 0 || indicesCount == 0)
					continue;

				// submit to batch
				MeshItem item = batcher.NextItem(verticesCount, indicesCount);
				item.texture = texture;
				for (int ii = 0, nn = indicesCount; ii < nn; ii++) {
					item.triangles[ii] = indices[ii];
				}
				VertexPositionColorTextureColor[] itemVertices = item.vertices;
				for (int ii = 0, v = 0, nn = verticesCount << 1; v < nn; ii++, v += 2) {
					itemVertices[ii].Color = color;
					itemVertices[ii].Color2 = darkColor;
					itemVertices[ii].Position.X = vertices[v];
					itemVertices[ii].Position.Y = vertices[v + 1];
					itemVertices[ii].Position.Z = 0;
					itemVertices[ii].TextureCoordinate.X = uvs[v];
					itemVertices[ii].TextureCoordinate.Y = uvs[v + 1];
				}

				clipper.ClipEnd(slot);
			}
			clipper.ClipEnd();
		}
	}
}
