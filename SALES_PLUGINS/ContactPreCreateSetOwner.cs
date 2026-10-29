
// <copyright file="ContactPreCreateSetOwner.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Linq;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// When a new Contact is being created, this plugin checks if the "Set Enterprise Owner" field is set to "Yes".
    /// If yes, it automatically sets the Contact's owner to the "Enterprise" team and then resets the field to "No".
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "Contact" - Secondary Entity: n/a
    /// Run as: Calling User - Execution Order: 1
    /// Stage: Pre Operation - Execution Mode: Synchronous - Deploy: Server
    ///
    /// </remarks>
    public class ContactPreCreateSetOwner : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracing.Trace("ContactPreCreateSetOwner Plugin started.");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity && entity.LogicalName == "contact")
                {
                    tracing.Trace("Target entity is Contact.");

                    if (entity.Attributes.Contains("stt_setenterpriseowner") && (bool)entity["stt_setenterpriseowner"] == true)
                    {
                        tracing.Trace("stt_setenterpriseowner = Yes. Setting owner to Enterprise team.");

                        var query = new QueryExpression("team")
                        {
                            ColumnSet = new ColumnSet("teamid"),
                            Criteria =
                            {
                                Conditions =
                                {
                                    new ConditionExpression("name", ConditionOperator.Equal, "Enterprise"),
                                },
                            },
                        };

                        var team = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                        if (team != null)
                        {
                            // Set owner
                            entity["ownerid"] = new EntityReference("team", team.Id);
                            tracing.Trace("Owner set to Enterprise team (ID: {0})", team.Id);

                            entity["stt_setenterpriseowner"] = false;
                            tracing.Trace("stt_setenterpriseowner field reset to No.");
                        }
                        else
                        {
                            tracing.Trace("Enterprise team not found.");
                            throw new InvalidPluginExecutionException("Enterprise Team not found.");
                        }
                    }
                    else
                    {
                        tracing.Trace("stt_setenterpriseowner is No. Plugin will not set owner.");
                    }
                }
            }
            catch (Exception ex)
            {
                tracing.Trace("ContactPreCreateSetOwner Plugin Error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException("Error setting Contact owner to Enterprise Team", ex);
            }

            tracing.Trace("ContactPreCreateSetOwner Plugin finished.");
        }
    }
}
