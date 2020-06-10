using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SOESupport;
using System.Collections.Generic;



namespace SOE_Utilita
{
    /// <summary>
    /// class of Helper
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// Converte una Feature Class, opportunamente filtrata in un oggetto RecordSet
        /// </summary>
        /// <param name="featureClass">feature class input</param>
        /// <param name="queryFilter">query filter</param>
        /// <returns>return Recordset</returns>
        internal static IRecordSet2 ConvertToRecordset(IFeatureClass featureClass, IQueryFilter2 queryFilter)
        {
            IRecordSet recordSet = new RecordSetClass();
            IRecordSetInit recordSetInit = recordSet as IRecordSetInit;
            recordSetInit.SetSourceTable(featureClass as ITable, queryFilter);

            return (IRecordSet2)recordSetInit;
        }

        /// <summary>
        /// Converte una Feature Class, opportunamente filtrata in un oggetto RecordSet
        /// </summary>
        /// <param name="featureClass"></param>
        /// <param name="spatialFilter"></param>
        /// <returns></returns>
        internal static IRecordSet2 ConvertToRecordset(IFeatureClass featureClass, ISpatialFilter spatialFilter)
        {
            IRecordSet recordSet = new RecordSetClass();
            IRecordSetInit recordSetInit = recordSet as IRecordSetInit;
            recordSetInit.SetSourceTable(featureClass as ITable, spatialFilter);

            return (IRecordSet2)recordSetInit;
        }

        /// <summary>
        /// Converte una lista di IGeometry in un array di JsonObjects
        /// </summary>
        /// <param name="geometries">list of IGeometry</param>
        /// <returns>array of JsonObject</returns>
        internal static object[] GetListJsonObjects(List<IGeometry> geometries)
        {
            List<JsonObject> jsonObjects = new List<JsonObject>();
            geometries.ForEach(g =>
            {
                jsonObjects.Add(Conversion.ToJsonObject(g));
            });

            return jsonObjects.ToArray();
        }
    }
}
