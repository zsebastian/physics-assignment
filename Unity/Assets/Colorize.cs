using UnityEngine;
using System.Collections;

namespace Pool
{
	public class Colorize : MonoBehaviour {

		public Color Color = Color.red;

		void Update () 
		{
			Mesh mesh = GetComponent<MeshFilter>().mesh;
			Vector3[] vertices = mesh.vertices;
			Color[] colors = new Color[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				colors[i] = Color;
			}
			mesh.colors = colors;
			gameObject.renderer.material.color = Color;
		}

	}
}