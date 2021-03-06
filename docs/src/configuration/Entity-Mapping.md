By default, members decorated with a `KeyAttribute` are not mapped, unless the mapping is a deep clone. Entity key values are usually generated by a data store, and some ORMs will ignore key value updates, or in some cases, throw an exception.

```cs
// Retrieve the existing Customer entity:
var customer = await dbContext
    .Customers
    .FirstOrDefaultAsync(c => c.Id == customerDto.Id);

if (customer == null)
{
    // Deal with Customer not found
}

// Update the Customer from the DTO - this does not map Customer.Id:
Mapper.Map(customerDto).Over(customer);

// Save the changes - no problems!
await dbContext.SaveChangesAsync();
```

There are circumstances where you might want to map entity keys, though. A typical case might be to [delete an entity by id](https://stackoverflow.com/questions/2471433/how-to-delete-an-object-by-id-with-entity-framework):

```cs
// Create a dummy Customer entity from the DTO:
var customerToDelete = Mapper.Map(customerDto).ToANew<Customer>();

// Attach then remove the Customer from the context:
dbContext.Customers.Attach(customer);
dbContext.Customers.Remove(customer);

// Save the changes - no Customer.Id, so does not work!
await dbContext.SaveChangesAsync();
```

### Mapping Keys

There's a number of options to change entity key mapping behaviour, depending on your needs.

To map keys by default **in all mappings**, use:

```cs
Mapper.WhenMapping.MapEntityKeys();

// - - - - - - - - - - - - - - - 

// Customer.Id will now be mapped:
var customerToDelete = Mapper.Map(customerDto).ToANew<Customer>();
```

To map keys by default when mapping **between two particular types**, use:

```cs
Mapper.WhenMapping
    .From<CustomerDto>() // Apply to CustomerDto mappings
    .ToANew<Customer>()  // Apply to Customer creations
    .MapEntityKeys();    // Map entity keys

// - - - - - - - - - - - - - - - 

// Customer.Id will now be mapped:
var customerToDelete = Mapper.Map(customerDto).ToANew<Customer>();
```

To map keys for an **individual mapping**, use:

```cs
// Map the DTO, including to Customer.Id:
var customerToDelete = Mapper
    .Map(customerDto)
    .ToANew<Customer>(cfg => cfg.MapEntityKeys());
```

## Opting Out

If the default behaviour is changed to map keys, key mapping can be disabled when necessary.

To ignore keys when mapping **between two particular types**, use:

```cs
// Map keys by default:
Mapper.WhenMapping.MapEntityKeys();

// Ignore entity keys when mapping from any
// source Type to a Customer:
Mapper.WhenMapping
    .To<Customer>()      // Apply to Customer mappings
    .IgnoreEntityKeys(); // Do not map entity keys
```

To ignore keys for an **individual mapping**, use:

```cs
// Map keys by default:
Mapper.WhenMapping.MapEntityKeys();

// - - - - - - - - - - - - - - - 

// Ignore entity keys for this mapping:
Mapper
    .Map(customerDto)
    .Over(customer, cfg => cfg.IgnoreEntityKeys());
```