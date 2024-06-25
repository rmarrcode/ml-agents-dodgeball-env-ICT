/**
 * Copyright 2019 Oskar Sigvardsson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GK
{
	public class ConvexHullTest : MonoBehaviour
	{

		public GameObject NewPrefab;

		void Start()
        {
			Init();
			//StartCoroutine(Init());
        }

		void Init()
		{
			var calc = new ConvexHullCalculator();
			var verts = new List<Vector3>();
			var tris = new List<int>();
			var normals = new List<Vector3>();
			var points = new List<Vector3>();

			//while (true)
			{
				points.Clear();

				foreach(Transform p in gameObject.transform)
				{
					points.Add(p.position);
				}
				Debug.Log("Number of points = " + points.Count);

				calc.GenerateHull(points, true, ref verts, ref tris, ref normals);

				var newMesh = Instantiate(NewPrefab);

				//newMesh.transform.SetParent(transform, false);
				newMesh.transform.localPosition = Vector3.zero;
				newMesh.transform.localRotation = Quaternion.identity;
				newMesh.transform.localScale = Vector3.one;

				Mesh mesh = new Mesh();
				mesh.SetVertices(verts);
				mesh.SetTriangles(tris, 0);
				mesh.SetNormals(normals);

				newMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
				newMesh.GetComponent<MeshCollider>().sharedMesh = mesh;

				Debug.Log("Mesh Vertices: " + mesh.vertexCount);

				//yield return new WaitForSeconds(0.5f);
			}
		}
	}
}
