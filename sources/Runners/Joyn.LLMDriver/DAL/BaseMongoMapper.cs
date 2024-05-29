namespace Joyn.LLMDriver.DAL
{
    public class BaseMongoMapper
    {
        public static string ConnectionString { get; set; }
        public static string DatabaseName { get; set; }

        public static void Startup(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            DatabaseName = databaseName;

            //So guids are readable in Mongo the same way they are in C#
            //BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            //Epocas, 27/03/2020 > this allows POCO classes to have less fields than those existing in the Mongo database
            var pack = new MongoDB.Bson.Serialization.Conventions.ConventionPack();
            pack.Add(new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true));
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register("My Solution Conventions", pack, t => true);
        }
    }
}
