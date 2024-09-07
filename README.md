# Trash Sorter

This is a mod that just filters extra items out of your inventory.

Due to the API and limitations on being realistic and working within the confines of the game, it's not as perfect as I would like, but it's still useful.
Sooo what can it do? Well simple. You put the Name of a thing that is a component/ore/ammo/ingot you want to limit in your system, and it will suck it out. That simple.

Control is almost entirely in the sorter's custom data. All you need is a controller block [1] (above 1 will not do anything) and however many trash sorters you would like. 
Do you want it to take the trash out into its own ready-to-go box? Go for it. 

Guide:
Do you need all possible entries done for you? In the trash sorter, just write [Guide], and it will give you all the entries you can use. 
How do you make your inventory not be counted in the global inventory counter? Just add a [Trash] tag to its name. And now it's gone and forgotten.

Setup of system guide:
1. Place trash controller.
2. Place a trash sorter and make it look into whatever inventory you want it to suck items in.
3. In the trash sorter, you need to start entries with the first line being <Trash filter ON>; it being off will make it not work. 
4. Tag that inventory as [trash], or it will suck items infinitely because it will consider the inventory you pump items in as valid.
5. Write the display name of an item you want to be filtered and let the system fill the data for you, or just write it in this format: "Display Name | Requested Amount | Overflow trigger amount"

However, there are some rules. First, if you want the item always to be sorted out, just set it normally in the filter. My mod ignores any values below 0, resetting them to 0. 
If your trigger amount is the same or below the request amount, it will force itself to be "Request Amount + Request Amount * 0.75"

Also, because of how sorters work, items you want to sort out must be in THOUSANDS. It has around 3k items+- of precision because of how sorters work. Unless I find a way how to override this it will be like that.
