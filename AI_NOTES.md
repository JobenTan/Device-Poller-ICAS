## What you asked the AI to do
1. Print the report layout, logic check
2. Create a skeleton for DevicePoller
3. Use it to better understand Net8 Libraries and xUnit

## One thing it got wrong or did badly, and how you noticed
1. The extracted data, since it can return null, int and double variable got issues
2. Loop excution stucked on the Timeout.Infinite

## What you changed yourself
1. Added '?' to accept null values
2. Used WaitAsync