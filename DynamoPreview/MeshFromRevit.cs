using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Collections;
using Revit.GeometryConversion;

namespace DynamoPreview
{
    public class ZeroTouchTest
    {
        static Document rDoc = null;
        static Options geoOpt;
        static View view;
        static List<int> elemsInView = new List<int>();
        static bool currentViewOnly;
        private static List<Autodesk.DesignScript.Geometry.Mesh> simpleMeshes = new List<Autodesk.DesignScript.Geometry.Mesh>();

        public static List<Autodesk.DesignScript.Geometry.Mesh> ElementMesh(List<Revit.Elements.Element> elems, bool currentView = false)
        {
            List<Autodesk.DesignScript.Geometry.Mesh> meshes = new List<Autodesk.DesignScript.Geometry.Mesh>();

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
            object retrievedMeshes = GetMeshes(elems);
            meshes = ObjToMesh(retrievedMeshes);            

            return meshes;
        }

        private static List<Autodesk.DesignScript.Geometry.Mesh> ObjToMesh(object meshData)
        {
            List<Autodesk.DesignScript.Geometry.Mesh> meshes = new List<Autodesk.DesignScript.Geometry.Mesh>();
            if (meshData is IList)
            {
                var data = (meshData as IList)[0];
                if (data.GetType() == typeof(Autodesk.DesignScript.Geometry.Mesh))
                {
                    foreach (var m in (meshData as IList))
                    {
                        meshes.Add(m as Autodesk.DesignScript.Geometry.Mesh);
                    }
                }
                else
                {
                    List<object> currentList = (List<object>)meshData;
                    foreach (object obj in currentList)
                    {
                        try
                        {
                            meshes.AddRange(ObjToMesh(obj));
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Forms.MessageBox.Show("Error:\n\n" + ex.Message);
                        }
                    }
                }
            }
            else
            {
                // Get the Mesh
                Autodesk.DesignScript.Geometry.Mesh mesh = meshData as Autodesk.DesignScript.Geometry.Mesh;
                if (mesh != null)
                {
                    meshes.Add(mesh);
                }
                else
                    meshes.Add(null);

            }
            return meshes;
        }

        private static object GetMeshes(object currentData)
        {
            object val = null;
            if (currentData is IList)
            {
                List<object> listData = new List<object>();
                List<Revit.Elements.Element> currentList = (List<Revit.Elements.Element>)currentData;
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
                List<Autodesk.DesignScript.Geometry.IndexGroup> indexGroups = new List<Autodesk.DesignScript.Geometry.IndexGroup>();

                foreach (Autodesk.DesignScript.Geometry.Mesh m in unjoinedMeshes)
                {
                    if (m == null)
                        continue;
                    int baseCount = vertices.Count;
                    foreach (Autodesk.DesignScript.Geometry.Point pt in m.VertexPositions)
                    {
                        vertices.Add(pt);
                    }
                    foreach (Autodesk.DesignScript.Geometry.IndexGroup ig in m.FaceIndices)
                    {
                        if (ig.Count == 3)
                        {
                            Autodesk.DesignScript.Geometry.IndexGroup iGroup = Autodesk.DesignScript.Geometry.IndexGroup.ByIndices((uint)(ig.A + baseCount), (uint)(ig.B + baseCount), (uint)(ig.C + baseCount));
                            indexGroups.Add(iGroup);
                        }
                        else
                        {
                            Autodesk.DesignScript.Geometry.IndexGroup iGroup = Autodesk.DesignScript.Geometry.IndexGroup.ByIndices((uint)(ig.A + baseCount), (uint)(ig.B + baseCount), (uint)(ig.C + baseCount), (uint)(ig.D + baseCount));
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
