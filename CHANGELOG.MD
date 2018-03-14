### 2018-03-14 Autogenerate CrmUnitOfWork

CrmUnitOfWork in the Kipon.Dynamics.Plugin/Entities folder is now separated in a Design source, generated by crmsvcutil and the existing source, and it has been marked partial.
this allow the entity repository classes to be generated.

To use this, you must add servicename="PluralName" to the filter.xml like below example:

```xml
<filter>
  <entities>
    <entity servicename="Accounts">account</entity>
    <entity servicename="Contacts">contact</entity>
    <entity servicename="Opportunities">opportunity</entity>
    <entity servicename="Quotes">quote</entity>
    <entity servicename="Salesorders">salesorder</entity>
  </entities>
</filter>
```