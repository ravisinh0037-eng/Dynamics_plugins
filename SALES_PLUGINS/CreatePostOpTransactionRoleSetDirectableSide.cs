// <copyright file="CreatePostOpTransactionRoleSetDirectableSide.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Plugin to handle the creation of a Transaction Role and update the associated transaction
    /// with the appropriate directable side based on the role code and MSA configuration.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "Transaction role" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 2
    ///     Stage: Post Operation - Execution Mode: Synchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreatePostOpTransactionRoleSetDirectableSide : IPlugin
    {
        // Constants for Transaction Type Codes
        private const int NewSales = 924510000;
        private const int Resale = 924510001;
        private const int Refinance = 924510002;

        // Constants for Directable Role Codes
        private const int Agent = 924510000;
        private const int Attorney = 924510001;

        // Constants for Directable Side Codes
        private const int ListingSide = 924510000;
        private const int BuyingSide = 924510001;
        private const int BothSides = 924510002;

        // Constants for Transaction Role Codes
        private const int ListingAgent = 924510000;
        private const int SellingAgent = 924510001;
        private const int BuyersAttorney = 924510008;
        private const int SellersAttorney = 924510009;
        private const int LoanOfficer = 924510002;
        private const int LendersAttorney = 924510010;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            try
            {
                // Ensure the plugin is triggered on the creation of a Transaction Role
                if (context.MessageName != "Create" || !context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity targetEntity))
                {
                    tracingService.Trace("Plugin not triggered on Create or Target is not an Entity.");
                    return;
                }

                // Ensure the target entity is the Transaction Role entity
                if (targetEntity.LogicalName != "stt_transactionrole")
                {
                    tracingService.Trace("Target entity is not stt_transactionrole.");
                    return;
                }

                tracingService.Trace("Transaction Role entity detected.");

                // Retrieve the role code from the transaction role
                if (!targetEntity.Contains("stt_rolecode"))
                {
                    tracingService.Trace("stt_rolecode is not present on the Transaction Role.");
                    return;
                }

                int roleCode = targetEntity.GetAttributeValue<OptionSetValue>("stt_rolecode").Value;

                // Check if the role code matches the required values
                if (!this.IsValidRoleCode(roleCode))
                {
                    tracingService.Trace($"Role Code {roleCode} does not match the required values. Exiting plugin.");
                    return;
                }

                // Retrieve the associated stt_transactionid
                if (!targetEntity.Contains("stt_transactionid") || !(targetEntity["stt_transactionid"] is EntityReference transactionRef))
                {
                    tracingService.Trace("stt_transactionid is not present on the Transaction Role.");
                    return;
                }

                tracingService.Trace($"Transaction ID: {transactionRef.Id}");

                // Retrieve the associated transaction record
                Entity transaction = service.Retrieve("stt_transaction", transactionRef.Id, new ColumnSet("stt_istransaction", "stt_directableside1id", "stt_directableside2id", "stt_transactiontypecode"));
                if (transaction == null || !transaction.Contains("stt_istransaction") || transaction.GetAttributeValue<bool>("stt_istransaction") || !transaction.Contains("stt_transactiontypecode"))
                {
                    tracingService.Trace("Transaction record stt_istransaction is missing or Transaction is type Transaction or doesn't contain Transaction Type.");
                    return;
                }

                int transactionTypeCode = transaction.GetAttributeValue<OptionSetValue>("stt_transactiontypecode").Value;

                if (transactionTypeCode == Refinance && (roleCode == LendersAttorney || roleCode == LoanOfficer))
                {
                    // Update the transaction with the directable side
                    this.UpdateTransactionWithDirectableSide(transaction,  targetEntity.GetAttributeValue<EntityReference>("stt_contactid"), service, tracingService);
                    return;
                }

                // Query for associated MSAs
                var query = new QueryExpression("stt_transactionmsa")
                {
                    TopCount = 50,
                    ColumnSet = new ColumnSet("stt_msaid"),
                };
                query.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transaction.Id);

                tracingService.Trace("Querying stt_transactionmsa for associated MSAs.");
                EntityCollection transactionMSAs = service.RetrieveMultiple(query);

                if (transactionMSAs.Entities.Count == 0)
                {
                    tracingService.Trace("No associated MSAs found for the transaction.");
                    return;
                }

                tracingService.Trace($"Found {transactionMSAs.Entities.Count} associated MSAs for the transaction.");

                // Process each MSA
                foreach (Entity transactionMSA in transactionMSAs.Entities)
                {
                    tracingService.Trace($"Processing MSA ID: {transactionMSA.Id}");
                    Entity msa = service.Retrieve("stt_metropolitanstatisticalarea", transactionMSA.GetAttributeValue<EntityReference>("stt_msaid").Id, new ColumnSet("stt_directablerolecode", "stt_directablesidecode"));
                    if (msa == null || !msa.Contains("stt_directablerolecode") || !msa.Contains("stt_directablesidecode"))
                    {
                        tracingService.Trace($"MSA with ID {transactionMSA.GetAttributeValue<EntityReference>("stt_msaid").Id} is missing required fields.");
                        continue;
                    }

                    int msaDirectableRoleCode = msa.GetAttributeValue<OptionSetValue>("stt_directablerolecode").Value;
                    int msaDirectableSideCode = msa.GetAttributeValue<OptionSetValue>("stt_directablesidecode").Value;

                    // Determine the directable side
                    EntityReference directableSide = this.GetDirectableSide(roleCode, msaDirectableRoleCode, msaDirectableSideCode, targetEntity, tracingService);
                    if (directableSide == null)
                    {
                        tracingService.Trace("No matching directable side found for this MSA.");
                        continue;
                    }

                    // Update the transaction with the directable side
                    this.UpdateTransactionWithDirectableSide(transaction, directableSide, service, tracingService);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"An error occurred: {ex.Message}");
                throw new InvalidPluginExecutionException($"An error occurred in the plugin: {ex.Message}", ex);
            }

            tracingService.Trace("Plugin execution completed.");
        }

        /// <summary>
        /// Checks if the role code is valid.
        /// </summary>
        private bool IsValidRoleCode(int roleCode)
        {
            return roleCode == ListingAgent || roleCode == SellingAgent || roleCode == BuyersAttorney || roleCode == SellersAttorney || roleCode == LoanOfficer || roleCode == LendersAttorney;
        }

        /// <summary>
        /// Determines the directable side based on role code and MSA values.
        /// </summary>
        private EntityReference GetDirectableSide(int roleCode, int msaDirectableRoleCode, int msaDirectableSideCode, Entity targetEntity, ITracingService tracingService)
        {
            if ((roleCode == ListingAgent && msaDirectableRoleCode == Agent && msaDirectableSideCode == ListingSide) ||
                (roleCode == SellingAgent && msaDirectableRoleCode == Agent && msaDirectableSideCode == BuyingSide) ||
                (roleCode == BuyersAttorney && msaDirectableRoleCode == Attorney && msaDirectableSideCode == BuyingSide) ||
                (roleCode == SellersAttorney && msaDirectableRoleCode == Attorney && msaDirectableSideCode == ListingSide) ||
                ((roleCode == ListingAgent || roleCode == SellingAgent) && msaDirectableRoleCode == Agent && msaDirectableSideCode == BothSides))
            {
                tracingService.Trace("Matching directable side found.");
                return new EntityReference("contact", targetEntity.GetAttributeValue<EntityReference>("stt_contactid").Id);
            }

            return null;
        }

        /// <summary>
        /// Updates the transaction with the directable side.
        /// </summary>
        private void UpdateTransactionWithDirectableSide(Entity transaction, EntityReference directableSide, IOrganizationService service, ITracingService tracingService)
        {
            Entity transactionToUpdate = new Entity("stt_transaction", transaction.Id);

            if (!transaction.Contains("stt_directableside1id") || transaction.GetAttributeValue<EntityReference>("stt_directableside1id") == null)
            {
                tracingService.Trace("Setting Directable Side 1.");
                transactionToUpdate["stt_directableside1id"] = directableSide;
            }
            else if (!transaction.Contains("stt_directableside2id") || transaction.GetAttributeValue<EntityReference>("stt_directableside2id") == null)
            {
                tracingService.Trace("Setting Directable Side 2.");
                transactionToUpdate["stt_directableside2id"] = directableSide;
            }
            else
            {
                tracingService.Trace("Both Directable Sides are already set. No update needed.");
                return;
            }

            service.Update(transactionToUpdate);
            tracingService.Trace("Transaction updated with Directable Side.");
        }
    }
}
