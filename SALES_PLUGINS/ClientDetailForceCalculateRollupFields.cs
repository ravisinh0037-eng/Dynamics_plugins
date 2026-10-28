
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VEER.Plugins
{
    public class ClientDetailForceCalculateRollupFields : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var recordId = context.PrimaryEntityId;
            try
            {
                CalculateRollupField(service, context.PrimaryEntityName, recordId, "stt_lasttpstransactiondate");
                CalculateRollupField(service, context.PrimaryEntityName, recordId, "stt_lastopentransactiondate");
                CalculateRollupField(service, context.PrimaryEntityName, recordId, "stt_lastappointmentdate");
                CalculateRollupField(service, context.PrimaryEntityName, recordId, "stt_nextappointmentdate");
                CalculateRollupField(service, context.PrimaryEntityName, recordId, "stt_lastphonecalldate");
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("an error occured in plugin ClientDetailForceCalculateRollupFields :" + ex.Message);
            }
        }

        public static Entity CalculateRollupField(IOrganizationService organizationService, string entityLogicalName, Guid recordId, string rollupFieldSchemaName)
        {
            var request = new CalculateRollupFieldRequest
            {
                Target = new EntityReference(entityLogicalName, recordId),
                FieldName = rollupFieldSchemaName
            };

            var response = (CalculateRollupFieldResponse)organizationService.Execute(request);
            return response.Entity;
        }
    }
}
