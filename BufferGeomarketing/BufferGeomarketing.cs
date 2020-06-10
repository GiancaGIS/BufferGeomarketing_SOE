// Copyright 2018 ESRI
// 
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
// 
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
// 
// See the use restrictions at <your Enterprise SDK install location>/userestrictions.txt.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Specialized;
using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SOESupport;

using SOE_Utilita;

//TODO: sign the project (project properties > signing tab > sign the assembly)
//      this is strongly suggested if the dll will be registered using regasm.exe <your>.dll /codebase


namespace BufferGeomarketing
{
    [ComVisible(true)]
    [Guid("3cde83eb-bd94-44c2-9a03-844b907546d2")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectExtension("MapServer",//use "MapServer" if SOE extends a Map service and "ImageServer" if it extends an Image service.
        AllCapabilities = "",
        DefaultCapabilities = "",
        Description = "",
        DisplayName = "BufferGeomarketing",
        Properties = "",
        SupportsREST = true,
        SupportsSOAP = false)]
    public class BufferGeomarketing : IServerObjectExtension, IObjectConstruct, IRESTRequestHandler
    {
        private string soe_name;

        private IPropertySet configProps;
        private IServerObjectHelper serverObjectHelper;
        private ServerLogger logger;
        private IRESTRequestHandler reqHandler;

        #region Variabili globali per analisi GIS
        private IFeatureClass fcFiliari = null;
        private IFeatureClass fcClienti = null;
        #endregion

        public BufferGeomarketing()
        {
            soe_name = this.GetType().Name;
            logger = new ServerLogger();
            reqHandler = new SoeRestImpl(soe_name, CreateRestSchema()) as IRESTRequestHandler;
        }

        #region IServerObjectExtension Members

        public void Init(IServerObjectHelper pSOH)
        {
            serverObjectHelper = pSOH;
        }

        public void Shutdown()
        {
        }

        #endregion

        #region IObjectConstruct Members

        public void Construct(IPropertySet props)
        {
            configProps = props;
        }

        #endregion

        #region IRESTRequestHandler Members

        public string GetSchema()
        {
            return reqHandler.GetSchema();
        }

        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName, string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            return reqHandler.HandleRESTRequest(Capabilities, resourceName, operationName, operationInput, outputFormat, requestProperties, out responseProperties);
        }

        #endregion

        private RestResource CreateRestSchema()
        {
            RestResource rootRes = new RestResource(soe_name, false, RootResHandler);

            RestOperation analisiGeomarketingBuffer = new RestOperation("analisiGeomarketing",
                                                      new string[] {  "OBJECTID", 
                                                                      "bufferCerchio1", 
                                                                      "bufferCerchio2", 
                                                                      "bufferCerchio3" },
                                                      new string[] { "json" },
                                                      AnalGeomBufferHandler);

            rootRes.operations.Add(analisiGeomarketingBuffer);

            RestOperation analisiGeomarketingSketch = new RestOperation("analisiGeomarketingSketch",
                                          new string[] {  "geomSketch" },
                                          new string[] { "json" },
                                          AnalisiGeomarketingSketchHandler);

            rootRes.operations.Add(analisiGeomarketingSketch); 
            
            return rootRes;
        }

        private byte[] RootResHandler(NameValueCollection boundVariables, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;

            JsonObject result = new JsonObject();

            return Encoding.UTF8.GetBytes(result.ToJson());
        }

        private byte[] AnalisiGeomarketingSketchHandler(NameValueCollection boundVariables,
                                          JsonObject operationInput,
                                              string outputFormat,
                                              string requestProperties,
                                          out string responseProperties)
        {
            #region Istanzio il JSON Result
            JsonObject result = new JsonObject();
            result.AddBoolean("hasError", false);
            #endregion

            this.RicavaInfoFc(); 
            
            responseProperties = null;

            IGeometry geomSketch = null;

            JsonObject jsonPoligono = null; // CoClass ESRI per object JSON

            // Controllo che il TAG nel JSON 'punto' di input ci sia:
            if (!operationInput.TryGetJsonObject("geomSketch", out jsonPoligono))
                throw new ArgumentNullException("geomSketch");

            geomSketch = Conversion.ToGeometry(jsonPoligono, esriGeometryType.esriGeometryPolygon) as IGeometry;

            if (geomSketch == null)
            {
                result.AddString("errorDescription", "GiancaGIS REST SOE Geometrie: punto invalido!");
                result.AddBoolean("hasError", true);

                throw new ArgumentException("GiancaGIS REST SOE Geometrie: punto invalido!");
            }

            try
            {
                IRecordSet2 recordSet = this.AnalisiSpazSketchOper(geomSketch);

                if (recordSet.IsFeatureCollection)
                {
                    result.AddJsonObject("clientiInterno",
                        new JsonObject(Encoding.UTF8.GetString(Conversion.ToJson(recordSet))));
                }

                else
                {
                    result.AddString("clientiInterno", "Nessun cliente nell'area selezionata!");
                }
            }
            catch (Exception errore)
            {
                result.AddString("errorDescription", errore.Message);
                result.AddBoolean("hasError", true);
            }

            
            return Encoding.UTF8.GetBytes(result.ToJson());
        }

        private byte[] AnalGeomBufferHandler(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            responseProperties = null;

            long? paramOID;
            bool found = operationInput.TryGetAsLong("OBJECTID", out paramOID);
            if (!found || !paramOID.HasValue)
                throw new ArgumentNullException("OBJECTID invalido!");

            long? paramBuffer1;
            found = operationInput.TryGetAsLong("bufferCerchio1", out paramBuffer1);
            if (!found || !paramBuffer1.HasValue)
                throw new ArgumentNullException("Valore Buffer1 invalido!!");

            long? paramBuffer2;
            found = operationInput.TryGetAsLong("bufferCerchio2", out paramBuffer2);
            if (!found || !paramBuffer2.HasValue)
                throw new ArgumentNullException("Valore Buffer2 invalido!!");

            long? paramBuffer3;
            found = operationInput.TryGetAsLong("bufferCerchio3", out paramBuffer3);
            if (!found || !paramBuffer3.HasValue)
                throw new ArgumentNullException("Valore Buffer3 invalido!!");

            this.RicavaInfoFc();

            IFeature feature = null;
            this.RicavaFeatureFiliare(paramOID.Value, out feature);

            Dictionary<long, IPolygon> dizGeometrieBuffer =
                this.CostruisciBuffer(feature, paramBuffer1.Value, paramBuffer2.Value, paramBuffer3.Value);

            List<IRecordSet2> listaRecordSet =
                this.AnalisiSpazialiBufferOper(dizGeometrieBuffer);

            JsonObject result = new JsonObject();

            result.AddJsonObject("interno", 
                new JsonObject(Encoding.UTF8.GetString(Conversion.ToJson(listaRecordSet[0]))));
            result.AddJsonObject("centrale",
                new JsonObject(Encoding.UTF8.GetString(Conversion.ToJson(listaRecordSet[1]))));

            result.AddJsonObject("superiore",
                new JsonObject(Encoding.UTF8.GetString(Conversion.ToJson(listaRecordSet[2]))));

            #region Mi occupo di tirare fuori le Geometrie dei Buffer circolari e serializzarle
            List<IGeometry> listaGeometrie = new List<IGeometry>();
            foreach (KeyValuePair<long, IPolygon> coppia in dizGeometrieBuffer)
            {
                // ATTENZIONE!!!

                // GLI ARCOBJECTS (METODO CONVERSION - SOE_SUPPORT) NON GESTISCE L'INSERIMENTO DI CURVE NEL JSON.
                // UNA TRUE CURVE DEVE ESSERE SEMPLIFICATA - DENSIFICATA COME UN INSIEME DI SEGMENTI (es: shapefile)
                // PRIMA DI ESSERE RESTITUITA AL CLIENT!!

                IPolycurve polycurve = coppia.Value as IPolycurve;
                polycurve.Densify(5, 0);

                IGeometry geo = polycurve as IGeometry;
                geo.SpatialReference = ((IGeoDataset)fcClienti).SpatialReference;

                listaGeometrie.Add(geo);
            }

            object[] objArray = Helper.GetListJsonObjects(listaGeometrie);
            #endregion

            result.AddArray("geomBuffer", objArray);

            return Encoding.UTF8.GetBytes(result.ToJson());
        }


        private IRecordSet2 AnalisiSpazSketchOper(IGeometry geometry)
        {
            IRecordSet2 recordSetPunti = null;
            try
            {
                ISpatialFilter spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = geometry;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                recordSetPunti = Helper.ConvertToRecordset(fcClienti, spatialFilter);

            }
            catch (Exception errore)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, $@"SOE GeoMarketing GiancaGIS: {errore.Message}");
                throw;
            }

            return recordSetPunti;
        }


        #region Logica Operazione SOE per i Buffer concentrici

        private List<IRecordSet2> AnalisiSpazialiBufferOper(Dictionary<long, IPolygon> dizBuffer)
        {
            List<IRecordSet2> listaRecordSetPunti = new List<IRecordSet2>();

            try
            {
                #region Analizzo tutti i punti che ricadono nel primo buffer

                // Primo buffer è un cerchio!
                ISpatialFilter spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = dizBuffer[0] as IGeometry;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                listaRecordSetPunti.Add(Helper.ConvertToRecordset(fcClienti, spatialFilter));

                #endregion

                #region Analizzo secondo Buffer!

                // Ricavo buffer simmetrico (ciambella) di differenza tra il primo ed il secondo cerchio!
                ITopologicalOperator topologicalOperator = dizBuffer[1] as ITopologicalOperator;
                IGeometry geomDiff1 = topologicalOperator.Difference(dizBuffer[0] as IGeometry);

                spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = geomDiff1;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                listaRecordSetPunti.Add(Helper.ConvertToRecordset(fcClienti, spatialFilter));
                #endregion

                #region Analizzo terzo Buffer!

                // Ricavo buffer simmetrico (ciambella) di differenza tra il primo ed il secondo cerchio!
                topologicalOperator = dizBuffer[2] as ITopologicalOperator;
                IGeometry geomDiff2 = topologicalOperator.Difference(dizBuffer[1] as IGeometry);

                spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = geomDiff2;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                listaRecordSetPunti.Add(Helper.ConvertToRecordset(fcClienti, spatialFilter));
                #endregion
            }
            catch (Exception errore)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, $@"SOE GeoMarketing GiancaGIS: {errore.Message}");
            }

            return listaRecordSetPunti;
        }

        #region Metodi per la costruzione delle Geometrie dei Buffer

        private IPolygon CostruisciPoligonoBuffer(double xCentro, double yCentro, long raggio)
        {
            IPolygon cerchio = null;
            try
            {
                IGeoDataset geoDataset = fcClienti as IGeoDataset;

                #region Costruisco i 4 punti cardine del cerchio
                IPoint centro = new PointClass();
                centro.SpatialReference = geoDataset.SpatialReference;
                centro.X = xCentro;
                centro.Y = yCentro;

                IPoint puntoNord = new PointClass();
                puntoNord.SpatialReference = geoDataset.SpatialReference;
                puntoNord.X = xCentro;
                puntoNord.Y = (yCentro + raggio);

                IPoint puntoSud = new PointClass();
                puntoSud.SpatialReference = geoDataset.SpatialReference;
                puntoSud.X = xCentro;
                puntoSud.Y = (yCentro - raggio);

                IPoint puntoOvest = new PointClass();
                puntoOvest.SpatialReference = geoDataset.SpatialReference;
                puntoOvest.X = (xCentro + raggio);
                puntoOvest.Y = yCentro;

                IPoint puntoEst = new PointClass();
                puntoEst.SpatialReference = geoDataset.SpatialReference;
                puntoEst.X = (xCentro - raggio);
                puntoEst.Y = yCentro;

                #endregion

                ISegmentCollection ringSegColl = new RingClass();

                ICircularArc circularArc = new CircularArcClass();
                circularArc.PutCoords(centro, puntoNord, puntoOvest, esriArcOrientation.esriArcClockwise);
                ringSegColl.AddSegment(circularArc as ISegment);

                ICircularArc circularArc2 = new CircularArcClass();
                circularArc2.PutCoords(centro, puntoOvest, puntoSud, esriArcOrientation.esriArcClockwise);
                ringSegColl.AddSegment(circularArc2 as ISegment);

                ICircularArc circularArc3 = new CircularArcClass();
                circularArc3.PutCoords(centro, puntoSud, puntoEst, esriArcOrientation.esriArcClockwise);
                ringSegColl.AddSegment(circularArc3 as ISegment);

                ICircularArc circularArc4 = new CircularArcClass();
                circularArc4.PutCoords(centro, puntoEst, puntoNord, esriArcOrientation.esriArcClockwise);
                ringSegColl.AddSegment(circularArc4 as ISegment);


                IRing ringl = ringSegColl as IRing;
                ringl.Close();

                IGeometryCollection polygon = new PolygonClass();
                polygon.AddGeometry(ringl as IGeometry);

                cerchio = polygon as IPolygon;
            }
            catch (Exception errore)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, $@"SOE GeoMarketing GiancaGIS: {errore.Message}");
            }


            return cerchio;
        }

        private Dictionary<long, IPolygon> CostruisciBuffer(IFeature featureFiliare, long buffer1, long buffer2, long buffer3)
        {
            Dictionary<long, IPolygon> diz = new Dictionary<long, IPolygon>();

            try
            {
                ITopologicalOperator topologicalOperator = featureFiliare.ShapeCopy as ITopologicalOperator;

                long[] listone = new long[3] { buffer1, buffer2, buffer3 };

                for (int i = 0; i < listone.Length; i++)
                {
                    IPolygon polygon = this.CostruisciPoligonoBuffer(featureFiliare.ShapeCopy.Envelope.XMax,
                        featureFiliare.ShapeCopy.Envelope.YMax, listone[i]);
                    diz.Add(i, polygon);
                };
            }
            catch (Exception errore)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, $@"SOE GeoMarketing GiancaGIS: {errore.Message}");
            }

            return diz;
        }

        #endregion

        private void RicavaFeatureFiliare(long OID, out IFeature feature)
        {
            IQueryFilter2 queryFilter = new QueryFilterClass();
            queryFilter.WhereClause = $@"OBJECTID = {OID}";

            IFeatureCursor featureCursor = fcFiliari.Search(queryFilter, true);
            feature = featureCursor.NextFeature();
            Marshal.ReleaseComObject(featureCursor);
        }

        #endregion

        private void RicavaInfoFc()
        {
            try
            {
                IMapServer4 mapServer = (IMapServer4)serverObjectHelper.ServerObject;
                string nomeMapService = mapServer.DefaultMapName;

                // Use IMapServerDataAccess to get the data
                IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
                // Get access to the source feature class.
                fcClienti = (IFeatureClass)dataAccess.GetDataSource(nomeMapService, 0);
                fcFiliari = (IFeatureClass)dataAccess.GetDataSource(nomeMapService, 1);

                logger.LogMessage(ServerLogger.msgType.infoDetailed, "Fc", 8000, "SOE Geomarketing GiancaGIS: FC ricavati");
            }
            catch (Exception errore)
            {
                logger.LogMessage(ServerLogger.msgType.error, "Construct", 8000, $@"SOE GeoMarketing GiancaGIS: {errore.Message}");
            }
        }

    }
}
