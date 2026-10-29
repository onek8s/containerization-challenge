# containerization-challenge

This repository is a collection of .NET/C# business applications that were originally built
for Windows. They were written in different eras, from the early .NET Framework days
(2.0/3.5-era technologies such as ASP.NET Web Forms, ASMX web services, WCF, .NET Remoting,
Enterprise Services and LINQ to SQL) through the .NET Framework 4.x years to modern .NET.
Each one uses the frameworks, libraries, hosting models and integration patterns that were
typical when it was created.

## The challenge

The goal is to build the best possible containerization tool. Given one of these
applications, the tool should:

- assess how easy or hard it is to refactor the application to run as Linux containers on
  Kubernetes, and explain why,
- present that assessment so a user can decide which applications are worth taking on, and
- help carry out the refactoring for the applications the user chooses.

About 50% of the applications purposely use technologies that are hard or impossible to
containerize. A good tool recognises these and says so, rather than producing a container
image that builds but does not work.

## Scenarios

Each folder under [`scenarios/`](scenarios) is one self-contained application:

| Scenario | Description |
| --- | --- |
| [account-statements](scenarios/account-statements) | Customer statements and receivables reporting for a leasing company. |
| [b2b-integration](scenarios/b2b-integration) | EDI, AS2 and partner order integration between customers, suppliers and the ERP of a wholesale distributor, with operations portal, customer tracking site and SLA monitoring. |
| [batch-release](scenarios/batch-release) | QA batch disposition and Qualified Person release for a pharmaceutical site. |
| [broker-quotes](scenarios/broker-quotes) | Online quoting, referrals and policy administration for an insurer selling through brokers. |
| [city-permits](scenarios/city-permits) | Building permits, business licences and parking permits for a city council: citizen portal, case officer back office, inspections app, map service, open data API and background services. |
| [clinic-scheduling](scenarios/clinic-scheduling) | Outpatient scheduling, patient portal, HL7 hospital and lab interfaces, claims and statements for a multi-site medical group. |
| [consumer-loans](scenarios/consumer-loans) | Consumer loan applications, credit decisions, e-signed agreements, loan servicing and collections for a savings bank with an IBM i core banking system. |
| [control-tower](scenarios/control-tower) | Freight forwarding operations: bookings, carrier EDI and API integrations, customs declarations, truck telematics, terminal scanning, customer tracking portal and a live control tower. |
| [credit-desk](scenarios/credit-desk) | Customer credit limit requests, approvals and signed confirmation letters. |
| [customer-care](scenarios/customer-care) | Case management for an energy company's contact centre. |
| [dealer-orders](scenarios/dealer-orders) | Dealer portal, sales desk and EDI for a machinery maker on Dynamics AX 2012. |
| [document-control](scenarios/document-control) | Engineering document review, approval and transmittals to clients and contractors. |
| [expense-claims](scenarios/expense-claims) | Travel and expense claims with receipts, company card purchases, approvals, bank payments and ERP journal export. |
| [field-sales](scenarios/field-sales) | Order taking for food wholesale sales reps with a desktop client that works offline, manager approvals and an ERP interface. |
| [field-service](scenarios/field-service) | Work order system for a facilities maintenance team. |
| [freight-rates](scenarios/freight-rates) | LTL freight quoting for a freight brokerage. |
| [furnace-records](scenarios/furnace-records) | Heat treatment load records, uniformity surveys and certificates for a thermal processing plant: portal, MES API, furnace acquisition service and worker service. |
| [housing-management](scenarios/housing-management) | Housing association platform: tenant portal, rent and direct debits, repairs with contractor API, compliance certificates, documents and scheduled jobs. |
| [label-station](scenarios/label-station) | Desktop tool for printing shipping labels at the packing desks. |
| [meter-billing](scenarios/meter-billing) | Water meter reading import and billing runs. |
| [order-hub](scenarios/order-hub) | Order integration hub for an outdoor-equipment wholesaler: webshop, marketplace and retail partner orders, 3PL warehouse over SFTP, invoicing, stock sync and an operations portal. |
| [parts-catalog](scenarios/parts-catalog) | Spare parts register for the maintenance stores. |
| [policy-changes](scenarios/policy-changes) | Insurance policy change requests from customer service and brokers. |
| [product-returns](scenarios/product-returns) | Online product returns, carrier labels, dock scanning and refunds. |
| [project-sites](scenarios/project-sites) | Engineering project collaboration sites, document control and project dashboards. |
| [property-inspections](scenarios/property-inspections) | Insurance claim inspection photos and reports. |
| [purchase-requests](scenarios/purchase-requests) | Purchase request approvals and purchase orders handed to the ERP. |
| [retail-backoffice](scenarios/retail-backoffice) | Omnichannel back office for a fashion retailer: catalog, pricing and promotions, inventory and allocation, orders, returns, loyalty, store operations and reporting. |
| [shift-clock](scenarios/shift-clock) | Time and attendance recording at the plant gates. |
| [staff-onboarding](scenarios/staff-onboarding) | Joiner, mover and leaver requests with user account provisioning. |
| [study-data](scenarios/study-data) | Clinical trial data capture and monitoring for a contract research organisation: site portal, monitoring app, sponsor API and jobs service. |
| [supplier-invoices](scenarios/supplier-invoices) | Accounts payable hub that collects and matches supplier invoices. |
| [trade-rebates](scenarios/trade-rebates) | Distributor rebate agreements, claims, accruals and statements. |

## Building and running

Each scenario's own README describes its requirements and how to build, deploy and run it
in its original Windows environment.
