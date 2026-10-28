
namespace VEER.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Prevents duplicate License Number records.
    /// Register on Create of stt_licensenumber,
    /// Pre-Operation, Synchronous.
    /// </summary>
    public class CreatePreOpLicenseNumberDuplicateCheck : IPlugin
    {
        private const string EntityName = "stt_licensenumber";

        private const string LicenseNumberField = "stt_licensenumber";
        private const string ContactField = "stt_contactid";
        private const string StateField = "stt_stateid";
        private const string TypeField = "stt_typecode";

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = null;
            Guid correlationId = Guid.Empty;

            try
            {
                tracingService = (ITracingService)serviceProvider.GetService(
                    typeof(ITracingService));

                tracingService?.Trace(
                    "CreatePreOpLicenseNumberDuplicateCheck: Plugin execution started.");

                var context = (IPluginExecutionContext)serviceProvider.GetService(
                    typeof(IPluginExecutionContext));

                correlationId = context.CorrelationId;

                tracingService?.Trace(
                    "CorrelationId: {0}; Message: {1}; Entity: {2}; Stage: {3}; Mode: {4}",
                    correlationId,
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage,
                    context.Mode);

                // Validate the registration context.
                if (!string.Equals(
                        context.MessageName,
                        "Create",
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        context.PrimaryEntityName,
                        EntityName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    tracingService?.Trace(
                        "Plugin skipped because the message or entity did not match.");

                    return;
                }

                if (!context.InputParameters.Contains("Target") ||
                    !(context.InputParameters["Target"] is Entity target))
                {
                    tracingService?.Trace(
                        "ERROR: Target entity was missing from InputParameters.");

                    throw new InvalidPluginExecutionException(
                        "Unable to validate the License Number record because the target record is missing.");
                }

                // Read the fields from the incoming record.
                string licenseNumber =
                    target.GetAttributeValue<string>(LicenseNumberField);

                EntityReference contact =
                    target.GetAttributeValue<EntityReference>(ContactField);

                EntityReference state =
                    target.GetAttributeValue<EntityReference>(StateField);

                OptionSetValue type =
                    target.GetAttributeValue<OptionSetValue>(TypeField);

                tracingService?.Trace(
                    "Input values: LicenseNumber={0}; ContactId={1}; StateId={2}; TypeValue={3}",
                    string.IsNullOrWhiteSpace(licenseNumber)
                        ? "<empty>"
                        : licenseNumber,
                    contact == null ? "<null>" : contact.Id.ToString(),
                    state == null ? "<null>" : state.Id.ToString(),
                    type == null ? "<null>" : type.Value.ToString());

                // If the fields needed for duplicate detection are not present,
                // allow other Dataverse validation to handle the record.
                if (string.IsNullOrWhiteSpace(licenseNumber) ||
                    contact == null ||
                    state == null ||
                    type == null)
                {
                    tracingService?.Trace(
                        "Duplicate check skipped because one or more duplicate-check fields are empty.");

                    return;
                }

                var factory = (IOrganizationServiceFactory)serviceProvider.GetService(
                    typeof(IOrganizationServiceFactory));

                // Use the calling user's privileges.
                IOrganizationService service =
                    factory.CreateOrganizationService(context.UserId);

                tracingService?.Trace(
                    "Checking for an existing matching License Number record.");

                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(LicenseNumberField),
                    TopCount = 1,
                };

                query.Criteria.AddCondition(
                    LicenseNumberField,
                    ConditionOperator.Equal,
                    licenseNumber);

                query.Criteria.AddCondition(
                    ContactField,
                    ConditionOperator.Equal,
                    contact.Id);

                query.Criteria.AddCondition(
                    StateField,
                    ConditionOperator.Equal,
                    state.Id);

                query.Criteria.AddCondition(
                    TypeField,
                    ConditionOperator.Equal,
                    type.Value);

                EntityCollection existingRecords =
                    service.RetrieveMultiple(query);

                tracingService?.Trace(
                    "Duplicate query completed. Matching records found: {0}",
                    existingRecords.Entities.Count);

                if (existingRecords.Entities.Count > 0)
                {
                    tracingService?.Trace(
                        "DUPLICATE DETECTED. LicenseNumber={0}; ContactId={1}; StateId={2}; TypeValue={3}",
                        licenseNumber,
                        contact.Id,
                        state.Id,
                        type.Value);

                    throw new InvalidPluginExecutionException(
                        string.Format(
                            "Duplicate License Number detected. " +
                            "License Number: {0}. " +
                            "A record with the same License Number, Contact, State, and Type already exists.",
                            licenseNumber));
                }

                tracingService?.Trace(
                    "No duplicate found. Dataverse can continue creating the License Number record.");

                tracingService?.Trace(
                    "CreatePreOpLicenseNumberDuplicateCheck: Plugin execution completed successfully.");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService?.Trace(
                    "BUSINESS VALIDATION ERROR. CorrelationId={0}; Message={1}",
                    correlationId,
                    ex.Message);

                // Preserve the duplicate validation message.
                throw;
            }
            catch (Exception ex)
            {
                tracingService?.Trace(
                    "UNEXPECTED ERROR in CreatePreOpLicenseNumberDuplicateCheck.");

                tracingService?.Trace(
                    "CorrelationId: {0}",
                    correlationId);

                tracingService?.Trace(
                    "Exception Type: {0}",
                    ex.GetType().FullName);

                tracingService?.Trace(
                    "Exception Message: {0}",
                    ex.Message);

                tracingService?.Trace(
                    "Stack Trace: {0}",
                    ex.StackTrace);

                if (ex.InnerException != null)
                {
                    tracingService?.Trace(
                        "Inner Exception: {0}",
                        ex.InnerException.ToString());
                }

                // Fail closed: do not allow creation when the duplicate
                // validation could not be completed.
                throw new InvalidPluginExecutionException(
                    "The License Number record could not be validated due to an unexpected error. Please contact your system administrator and provide Correlation ID: " +
                    correlationId + ".");
            }
        }
    }
}
