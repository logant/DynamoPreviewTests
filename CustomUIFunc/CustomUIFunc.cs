using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamoServices;
using Autodesk.DesignScript.Runtime;
using Autodesk.DesignScript.Geometry;
using Autodesk.Revit.DB;
using System.Collections;
using Revit.GeometryConversion;
using Autodesk.DesignScript.Interfaces;

namespace CustomUIFunc
{
    [IsVisibleInDynamoLibrary(false)]
    public class MeshFromElementFunc
    {
        static Document rDoc = null;
        static Options geoOpt;
        static View view;
        static List<int> elemsInView = new List<int>();
        static bool currentViewOnly;

        private static List<Autodesk.DesignScript.Geometry.Mesh> simpleMeshes = new List<Autodesk.DesignScript.Geometry.Mesh>();


        /// <summary>
        /// This method just creates a 10x10x10 cube and outputs it if the bool input is set to true.
        /// This was just a simple test to see if it would show up.
        /// </summary>
        public static Autodesk.DesignScript.Geometry.Mesh GetSimpleMesh(bool returnBox)
        {
            // Build vertex list
            List<Autodesk.DesignScript.Geometry.Point> vertices = new List<Autodesk.DesignScript.Geometry.Point>();
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(0, 0, 0));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(10, 0, 0));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(10, 10, 0));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(0, 10, 0));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(0, 0, 10));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(10, 0, 10));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(10, 10, 10));
            vertices.Add(Autodesk.DesignScript.Geometry.Point.ByCoordinates(0, 10, 10));

            // build mesh faces
            List<IndexGroup> faces = new List<IndexGroup>();
            faces.Add(IndexGroup.ByIndices(3, 2, 1, 0));
            faces.Add(IndexGroup.ByIndices(4, 5, 1, 0));
            faces.Add(IndexGroup.ByIndices(5, 6, 2, 1));
            faces.Add(IndexGroup.ByIndices(6, 7, 3, 2));
            faces.Add(IndexGroup.ByIndices(7, 4, 0, 3));
            faces.Add(IndexGroup.ByIndices(7, 6, 5, 4));

            // create the mesh
            Autodesk.DesignScript.Geometry.Mesh mesh = Autodesk.DesignScript.Geometry.Mesh.ByPointsFaceIndices(vertices, faces);

            if (returnBox)
                return mesh;
            else
                return null;
        }
               
        public static object GetMeshes(List<Revit.Elements.Element> elements, bool currentView)
        {
            currentViewOnly = currentView;
            rDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;
            geoOpt = rDoc.Application.Create.NewGeometryOptions();
            geoOpt.ComputeReferences = true;
            geoOpt.IncludeNonVisibleObjects = false;

            if (currentView)
            {

                elemsInView = new List<int>();
                view = rDoc.ActiveView;
                foreach (ElementId eid in new FilteredElementCollector(rDoc, view.Id).WhereElementIsNotElementType().ToElementIds())
                {
                    elemsInView.Add(eid.IntegerValue);
                }

                geoOpt.View = view;
            }


            List<object> meshData = new List<object>();
            foreach (object currentObj in elements)
            {

                meshData.Add(GetMeshes(currentObj));
            }

            return meshData;
        }

        public static Autodesk.DesignScript.Geometry.Mesh GetMeshFromElem(Revit.Elements.Element element)
        {
            rDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;
            geoOpt = rDoc.Application.Create.NewGeometryOptions();
            geoOpt.ComputeReferences = true;
            geoOpt.IncludeNonVisibleObjects = false;


            object retrievedMeshes = GetMeshes(element);

            Autodesk.DesignScript.Geometry.Mesh m = null;
            if (retrievedMeshes is IList)
            {
                int counter = 0;
                while (retrievedMeshes is IList)
                {
                    List<Autodesk.DesignScript.Geometry.Mesh> listData = (List<Autodesk.DesignScript.Geometry.Mesh>)retrievedMeshes;
                    retrievedMeshes = listData[0];
                    counter++;
                    if (counter > 100)
                        break;
                }
                m = retrievedMeshes as Autodesk.DesignScript.Geometry.Mesh;
            }
            else
                m = retrievedMeshes as Autodesk.DesignScript.Geometry.Mesh;

            return m;
        }

        private static object GetMeshes(object currentData)
        {
            object val = null;
            if (currentData is IList)
            {
                List<object> listData = new List<object>();
                List<object> currentList = (List<object>)currentData;
                foreach (object obj in currentList)
                {
                    listData.Add(GetMeshes(obj));
                }
                val = listData;
            }
            else
            {
                // Get the Mesh
                Revit.Elements.Element elem = currentData as Revit.Elements.Element;
                if (elem != null)
                {
                    if (currentViewOnly && !elemsInView.Contains(elem.Id))
                        val = null;
                    else
                    {
                        Element revElem = rDoc.GetElement(new ElementId(elem.Id));
                        if (revElem is FamilyInstance)
                        {

                            FamilyInstance fi = revElem as FamilyInstance;
                            GeometryElement geoElem = fi.get_Geometry(geoOpt);
                            GeometryInstance geoInst = null;
                            List<Autodesk.DesignScript.Geometry.Mesh> instMeshes = new List<Autodesk.DesignScript.Geometry.Mesh>();
                            foreach (GeometryInstance gObj in geoElem)
                            {
                                geoInst = gObj;
                            }
                            if (geoInst != null)
                            {
                                GeometryElement gElem = geoInst.GetInstanceGeometry();
                                foreach (GeometryObject gObj in gElem)
                                {
                                    Autodesk.Revit.DB.Solid solid = gObj as Autodesk.Revit.DB.Solid;
                                    if (solid != null && solid.SurfaceArea > 0.01)
                                    {
                                        instMeshes.Add(SolidToMesh(solid));
                                    }
                                }
                            }
                            val = instMeshes;
                        }
                        else // Should be a System Family
                        {
                            GeometryElement geoElem = revElem.get_Geometry(geoOpt);
                            List<Autodesk.DesignScript.Geometry.Mesh> instMeshes = new List<Autodesk.DesignScript.Geometry.Mesh>();
                            foreach (GeometryObject gObj in geoElem)
                            {
                                Autodesk.Revit.DB.Solid solid = gObj as Autodesk.Revit.DB.Solid;
                                if (solid != null && solid.SurfaceArea > 0.01)
                                {
                                    instMeshes.Add(SolidToMesh(solid));
                                }
                            }

                            if (instMeshes.Count > 0)
                                val = instMeshes;
                        }
                    }
                }
                else
                    val = null;

            }

            return val;
        }

        private static Autodesk.DesignScript.Geometry.Mesh SolidToMesh(Autodesk.Revit.DB.Solid solid)
        {
            Autodesk.DesignScript.Geometry.Mesh mesh = null;

            List<Autodesk.DesignScript.Geometry.Mesh> unjoinedMeshes = new List<Autodesk.DesignScript.Geometry.Mesh>();
            foreach (Autodesk.Revit.DB.Face f in solid.Faces)
            {
                Autodesk.Revit.DB.Mesh rMesh = f.Triangulate();
                Autodesk.DesignScript.Geometry.Mesh dMesh = RevitToProtoMesh.ToProtoType(rMesh, true);
                unjoinedMeshes.Add(dMesh);
            }

            // Join meshes
            if (unjoinedMeshes.Count == 0)
            {
                mesh = unjoinedMeshes[0];
            }
            else
            {
                // Join all of the meshes?
                List<Autodesk.DesignScript.Geometry.Point> vertices = new List<Autodesk.DesignScript.Geometry.Point>();
                List<IndexGroup> indexGroups = new List<IndexGroup>();

                foreach (Autodesk.DesignScript.Geometry.Mesh m in unjoinedMeshes)
                {
                    if (m == null)
                        continue;
                    int baseCount = vertices.Count;
                    foreach (Autodesk.DesignScript.Geometry.Point pt in m.VertexPositions)
                    {
                        vertices.Add(pt);
                    }
                    foreach (IndexGroup ig in m.FaceIndices)
                    {
                        if (ig.Count == 3)
                        {
                            IndexGroup iGroup = IndexGroup.ByIndices((uint)(ig.A + baseCount), (uint)(ig.B + baseCount), (uint)(ig.C + baseCount));
                            indexGroups.Add(iGroup);
                        }
                        else
                        {
                            IndexGroup iGroup = IndexGroup.ByIndices((uint)(ig.A + baseCount), (uint)(ig.B + baseCount), (uint)(ig.C + baseCount), (uint)(ig.D + baseCount));
                            indexGroups.Add(iGroup);
                        }
                    }
                }
                try
                {
                    Autodesk.DesignScript.Geometry.Mesh joinedMesh = Autodesk.DesignScript.Geometry.Mesh.ByPointsFaceIndices(vertices, indexGroups);
                    if (joinedMesh != null)
                    {
                        mesh = joinedMesh;
                        simpleMeshes.Add(joinedMesh);
                    }
                }
                catch
                {
                    // For now just add them all as is
                }
            }
            return mesh;
        }
    }
}
