targetScope = 'subscription'

param contactEmails array
@minValue(1)
param monthlyAmount int = 150

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'gbv-shield-monthly-guardrail'
  properties: {
    category: 'Cost'
    amount: monthlyAmount
    timeGrain: 'Monthly'
    timePeriod: { startDate: '2026-09-01T00:00:00Z', endDate: '2027-09-09T00:00:00Z' }
    notifications: {
      Actual50: { enabled: true, operator: 'GreaterThan', threshold: 50, thresholdType: 'Actual', contactEmails: contactEmails }
      Actual75: { enabled: true, operator: 'GreaterThan', threshold: 75, thresholdType: 'Actual', contactEmails: contactEmails }
      Actual90: { enabled: true, operator: 'GreaterThan', threshold: 90, thresholdType: 'Actual', contactEmails: contactEmails }
      Forecast100: { enabled: true, operator: 'GreaterThan', threshold: 100, thresholdType: 'Forecasted', contactEmails: contactEmails }
    }
  }
}
