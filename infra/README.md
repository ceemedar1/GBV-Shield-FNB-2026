# Azure infrastructure

These templates are intentionally not deployed while the sponsorship subscription is unavailable.

After support confirms activation, validate and deploy with the nonprofit subscription explicitly selected. Deploy `budget.bicep` first, then `main.bicep`. The default monthly budget is USD 150; change it to match the organisation's approved operating limit. A budget sends alerts but does **not** automatically stop resources.

The Container App scales to zero and is capped at two replicas. Table Storage holds only active tracking records. Add an automated daily cleanup job before production to delete expired rows, because Table Storage has no native row TTL.
