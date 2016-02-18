﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEditor;


namespace GPUNoise
{
	public static class GraphEditorUtils
	{
		/// <summary>
		/// The standard file extension for Graphs.
		/// </summary>
		public static readonly string Extension = "gpug";


		/// <summary>
		/// Gets the full paths for all graphs in this Unity project.
		/// </summary>
		public static List<string> GetAllGraphsInProject()
		{
			DirectoryInfo inf = new DirectoryInfo(Application.dataPath);
			FileInfo[] files = inf.GetFiles("*." + Extension, SearchOption.AllDirectories);
			return files.Select(f => f.FullName).ToList();
		}


		/// <summary>
		/// Loads a graph from the given file.
		/// Returns "null" and prints to the Debug console if there was an error.
		/// </summary>
		public static Graph LoadGraph(string filePath)
		{
			IFormatter formatter = new BinaryFormatter();
			Stream s = null;
			Graph g = null;

			try
			{
				s = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				g = (Graph)formatter.Deserialize(s);
			}
			catch (System.Exception e)
			{
				UnityEngine.Debug.LogError("Error opening file: " + e.Message);
			}
			finally
			{
				s.Close();
			}

			return g;
		}
		/// <summary>
		/// Saves the given graph to the given file, overwriting it if it exists.
		/// Prints to the Debug console and returns false if there was an error.
		/// Otherwise, returns true.
		/// </summary>
		public static bool SaveGraph(Graph g, string filePath)
		{
			IFormatter formatter = new BinaryFormatter();
			Stream stream = null;
			bool failed = false;
			try
			{
				stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
				formatter.Serialize(stream, g);
			}
			catch (System.Exception e)
			{
				failed = true;
				UnityEngine.Debug.LogError("Error opening/writing to file: " + e.Message);
			}
			finally
			{
				if (stream != null)
				{
					stream.Close();
				}
			}

			return !failed;
		}

		/// <summary>
		/// Saves the shader for the given graph to the given file with the given name.
		/// Also forces Unity to immediately recognize and compile the shader
		///     so that it can be immediately used after calling this function.
		/// If the shader fails to load for some reason, an error is output to the Unity console
		///     and "null" is returned.
		/// </summary>
		/// <param name="outputComponents">
		/// The texture output. Can optionally output into a subset of the full RGBA components.
		/// For example, greyscale textures only need "r".
		/// </param>
		/// <param name="defaultColor">
		/// The color (generally 0-1) of the color components which aren't set by the noise.
		/// </param>
		public static Shader SaveShader(Graph g, string filePath,
										string shaderName = "GPU Noise/My Shader",
										string outputComponents = "rgb",
										float defaultColor = 0.0f)
		{
			string relativePath = PathUtils.GetRelativePath(filePath, "Assets");

			try
			{
				//Get the shader code.
				string shad = g.GenerateShader(shaderName, outputComponents, defaultColor);
				if (shad == null)
				{
					return null;
				}

				//Write to the file.
				File.WriteAllText(filePath, shad);
				

				//Tell Unity to load/compile it.
				AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
			}
			catch (Exception e)
			{
				Debug.LogError("Error saving/loading shader to/from file: " + e.Message);
			}

			return AssetDatabase.LoadAssetAtPath<Shader>(relativePath);
		}
		
		/// <summary>
		/// Generates a texture containing the given graph's noise output.
		/// If this is being called very often, create a permanent render target and material and
		///     use the other version of this method instead for much better performance.
		/// If an error occurred, outputs to the Unity debug console and returns "null".
		/// </summary>
		/// <param name="outputComponents">
		/// The texture output. Can optionally output into a subset of the full RGBA components.
		/// For example, greyscale textures only need "r".
		/// </param>
		/// <param name="defaultColor">
		/// The color (generally 0-1) of the color components which aren't set by the noise.
		/// </param>
		public static Texture2D GenerateToTexture(Graph g, int width, int height,
												  string outputComponents = "rgb",
												  float defaultColor = 0.0f,
												  TextureFormat format = TextureFormat.RGBAFloat)
		{
			//Generate a shader from the graph and have Unity compile it.
			string shaderPath = Path.Combine(Application.dataPath, "gpuNoiseShaderTemp.shader");
			Shader shader = SaveShader(g, shaderPath, "TempGPUNoiseShader", outputComponents, defaultColor);
			if (shader == null)
			{
				return null;
			}

			//Render the shader's output into a render texture and copy the data to a Texture2D.
			RenderTexture target = new RenderTexture(width, height, 16, RenderTextureFormat.ARGBFloat);
			target.Create();
			Texture2D resultTex = new Texture2D(width, height, format, false, true);

			//Create the material and set its parameters.
			Material mat = new Material(shader);
			new GraphParamCollection(g).SetParams(mat);

			GraphUtils.GenerateToTexture(target, new Material(shader), resultTex);

			//Clean up.
			target.Release();
			if (!AssetDatabase.DeleteAsset(PathUtils.GetRelativePath(shaderPath, "Assets")))
			{
				Debug.LogError("Unable to delete temp file: " + shaderPath);
			}

			return resultTex;
		}

		/// <summary>
		/// Generates a 2D grid of noise from the given graph.
		/// If an error occurred, outputs to the Unity debug console and returns "null".
		/// </summary>
		public static float[,] GenerateToArray(Graph g, int width, int height)
		{
			Texture2D t = GenerateToTexture(g, width, height, "r");
			if (t == null)
			{
				return null;
			}

			Color[] cols = t.GetPixels();
			float[,] vals = new float[width, height];
			for (int i = 0; i < cols.Length; ++i)
			{
				int x = i % width,
					y = i / width;

				vals[x, y] = cols[i].r;
			}

			return vals;
		}
	}
}