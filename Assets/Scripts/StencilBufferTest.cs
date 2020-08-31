using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace MediaMonks
{
	[ExecuteAlways]
    public class StencilBufferTest : MonoBehaviour
    {
		public enum Mode
		{
			Unicode,
			GlyphId
		};
		public Mode TestMode = Mode.Unicode;

		public string UnicodeCharacter = "A";
		public int GlyphId = 0;
		public bool animateGlyphIdForTest = false;
		public Color TextColor = Color.white;
		public Color BackgroundColor = Color.black;

		public RenderTexture Output;
		public Material TriangleFanMaterial;
		public Material CurveMaterial;
		public Material DisplayMaterial;

		[Range(0,1)]
		public float VertexScale = 1.0f;

		public Vector3 VertexOffset = Vector3.zero;
		public OpenFontFileRead FontReader;

		private int _previousGlyphId = -1;
		private string _previousUnicodeCharacter = "";
		private bool _forceUpdate = false;

		private void OnEnable()
		{
			_forceUpdate = true;
		}

		private void OnValidate()
		{
			_forceUpdate = true;
			GlyphId = GlyphId < 0 ? 0 : GlyphId;
			if (FontReader != null)
			{
				GlyphId = GlyphId > FontReader.Maximums.numberOfGlyphs ? FontReader.Maximums.numberOfGlyphs : GlyphId;
			}
		}

		private void Update()
		{
			if (animateGlyphIdForTest)
			{
				++GlyphId;
			}

			_forceUpdate = true;
			List<Glyph> glyphs = null;
			if (FontReader)
			{
				switch (TestMode)
				{
					case Mode.Unicode:
					{
						if ((_previousUnicodeCharacter != UnicodeCharacter || _forceUpdate) && UnicodeCharacter.Length > 0)
						{
							_forceUpdate = false;
							glyphs = new List<Glyph>();
							FontReader.GetControlPoints(UnicodeCharacter, glyphs);
							_previousUnicodeCharacter = UnicodeCharacter;
						}
						break;
						}
					case Mode.GlyphId:
					{
						if (_previousGlyphId != GlyphId || _forceUpdate)
						{
							_forceUpdate = false;
							glyphs = new List<Glyph>();
							FontReader.GetControlPoints(GlyphId, glyphs);
							_previousGlyphId = GlyphId;
						}
						break;
					}
				}
			}

			if (Output == null || TriangleFanMaterial == null || DisplayMaterial == null || glyphs == null)
			{
				return;
			}

			bool oldSRGBValue = GL.sRGBWrite;
			GL.sRGBWrite = true;
			Graphics.SetRenderTarget(Output);
			GL.Clear(true, true, BackgroundColor);
			DisplayMaterial.color = TextColor;

			for (int glyphIndex = 0; glyphIndex < glyphs.Count; ++glyphIndex)
			{
				Glyph glyph = glyphs[glyphIndex];

				#region PreProcessing
				List<Vector3> CurveTriangleVertices = new List<Vector3>();
				List<Vector3> TriangleFanVertices = new List<Vector3>();
				{
					int contourStartIndex = 0;
					for (int contourIndex = 0; contourIndex < glyph.EndIndices.Count; ++contourIndex)
					{
						List<Vector3> Vertices = new List<Vector3>();
						int contourEndIndex = glyph.EndIndices[contourIndex];
						int controlPointCount = contourEndIndex - contourStartIndex + 1;

						bool lastControlPointWasOnCurve = true;
						bool needLastCurveControlPoint = false;
						Vector3 lastControlPoint = Vector3.zero;

						for (int controlPointIndex = 0; controlPointIndex < controlPointCount; ++controlPointIndex)
						{
							bool onCurve = glyph.OnCurve[contourStartIndex + controlPointIndex];

							if (controlPointIndex == 0 && !onCurve)
							{
								if (TestMode == Mode.Unicode)
								{
									Debug.LogError("The first control point is off-curve, which is unexpected. Character: " + _previousUnicodeCharacter, this);// If this gets called, the font works differently than expected during development, and the following algorithms might have to be revised.
								}
								else
								{
									Debug.LogError("The first control point is off-curve, which is unexpected. Glyph ID: " + _previousGlyphId, this);// If this gets called, the font works differently than expected during development, and the following algorithms might have to be revised.
								}
								return;
							}

							Vector2 controlPoint = glyph.ControlPoints[contourStartIndex + controlPointIndex];
							Vector3 controlPointV3 = new Vector3(controlPoint.x, controlPoint.y, 0);

							if (onCurve)
							{
								Vertices.Add(controlPointV3);
								if (needLastCurveControlPoint)
								{
									needLastCurveControlPoint = false;
									CurveTriangleVertices.Add(controlPointV3);
								}
							}
							else
							{
								if (!needLastCurveControlPoint)
								{
									needLastCurveControlPoint = true;
									CurveTriangleVertices.Add(Vertices[Vertices.Count - 1]);
									CurveTriangleVertices.Add(controlPointV3);
								}

								if (!lastControlPointWasOnCurve)
								{
									Vector3 intermediateControlPoint = lastControlPoint + ((controlPointV3 - lastControlPoint) * 0.5f);
									Vertices.Add(intermediateControlPoint);

									if (needLastCurveControlPoint)
									{
										CurveTriangleVertices.Add(intermediateControlPoint);
										// start the new curve:
										CurveTriangleVertices.Add(intermediateControlPoint);
										CurveTriangleVertices.Add(controlPointV3);
									}
								}
							}

							lastControlPoint = controlPointV3;
							lastControlPointWasOnCurve = onCurve;
						}

						if (needLastCurveControlPoint)
						{
							CurveTriangleVertices.Add(Vertices[0]);
							needLastCurveControlPoint = false;
						}

						for (int vertexIndex = 0; vertexIndex < Vertices.Count; ++vertexIndex)
						{
							int second = (vertexIndex + 1) % Vertices.Count;
							int third = (vertexIndex + 2) % Vertices.Count;

							TriangleFanVertices.Add(Vertices[0]);
							TriangleFanVertices.Add(Vertices[second]);
							TriangleFanVertices.Add(Vertices[third]);
						}
						contourStartIndex = contourEndIndex + 1;
					}
				}
				#endregion

				#region Draw Processed Vertices to stencil
				GL.PushMatrix();
				bool passSuccessfullySet = TriangleFanMaterial.SetPass(0);
				if (!passSuccessfullySet)
				{
					Debug.LogError("Could not set pass", this);
					return;
				}
				GL.LoadOrtho();

				GL.Begin(GL.TRIANGLES);
				Profiler.BeginSample("Draw Triangle Fan");
				for (int vertexIndex = 0; vertexIndex < TriangleFanVertices.Count; ++vertexIndex)
				{
					GL.Vertex(VertexOffset + TriangleFanVertices[vertexIndex] * VertexScale);
				}
				Profiler.EndSample();
				GL.End();
				#endregion

				#region Curves
				passSuccessfullySet = CurveMaterial.SetPass(0);
				if (!passSuccessfullySet)
				{
					Debug.LogError("Failed to set curve material pass", this);
					return;
				}

				GL.Begin(GL.TRIANGLES);
				Profiler.BeginSample("Draw Bezier Curves");
				for (int vertexIndex = 0; vertexIndex < CurveTriangleVertices.Count; vertexIndex += 3)
				{
					GL.TexCoord2(0, 0);
					GL.Vertex(VertexOffset + CurveTriangleVertices[vertexIndex + 0] * VertexScale);

					GL.TexCoord2(0.5f, 0);
					GL.Vertex(VertexOffset + CurveTriangleVertices[vertexIndex + 1] * VertexScale);

					GL.TexCoord2(1, 1);
					GL.Vertex(VertexOffset + CurveTriangleVertices[vertexIndex + 2] * VertexScale);
				}
				Profiler.EndSample();

				GL.End();
				#endregion

				#region Draw stencil to quad
				passSuccessfullySet = DisplayMaterial.SetPass(0);

				if (!passSuccessfullySet)
				{
					Debug.LogError("Could not set pass");
					return;
				}

				GL.Begin(GL.QUADS);
				GL.Vertex3(0, 0, 0);
				GL.Vertex3(1, 0, 0);
				GL.Vertex3(1, 1, 0);
				GL.Vertex3(0, 1, 0);
				GL.End();

				GL.PopMatrix();

				#endregion

				GL.Clear(true, false, BackgroundColor);
			}

			GL.sRGBWrite = oldSRGBValue;
		}
	}
}