using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Operations;

namespace Cactus.Mongo.Migration
{
    public static class MongoDatabaseExtensions
    {
        /// <summary>
        /// Evaluates the specified javascript within a MongoDb database.
        /// For some reasons MongoDB driver stopped to support this feature. This method is workaround that uses some internal-use-only features.
        /// WARN: method doesn't support transactions. Wrong script may update db partly and then fail. Changes won't be rolled back.
        /// WARN: eval operation requires 'anyResource-anyAction' permission, see the script below
        /// db.createRole( { role: "executeFunctions", privileges: [ { resource: { anyResource: true }, actions: [ "anyAction" ] } ], roles: [] } )
        /// db.grantRolesToUser("USERNAME", [ { role: "executeFunctions", db: "admin" } ])
        /// WARN: Don't keep the permission after update, revoke it: 
        /// db.revokeRolesFromUser("USERNAME",[ { role: "executeFunctions", db: "admin" } ])
        /// </summary>
        /// <param name="database">Mongodb database to execute the javascript</param>
        /// <param name="session">Session for tx support</param>
        /// <param name="javascript">Javascript to execute</param>
        /// <returns>A BsonValue result</returns>
        public static async Task<BsonValue> EvalAsync(this IMongoDatabase database, IClientSessionHandle session,
            string javascript)
        {
            var client = database.Client as MongoClient;

            if (client == null)
                throw new ArgumentException("Client is not a MongoClient");

            var function = new BsonJavaScript(javascript);
            var op = new EvalOperation(database.DatabaseNamespace, function, null);

            using (var writeBinding =
                new WritableServerBinding(client.Cluster, new CoreSessionHandle(NoCoreSession.Instance))) //new CoreSessionHandle(NoCoreSession.WrappedCoreSession)
            {
                return await op.ExecuteAsync(writeBinding, CancellationToken.None);
            }
        }
    }
}
