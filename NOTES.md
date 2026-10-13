##  What you would do differently with more time, and what you did not finish or are not happy with. 

I would add in more test cases, and validation test cases as well. Currently max attempts is 3, I can add more configurations to the attempts based on the device specifications. Also like other readings immediate stops and does not consider network downs or errors.

I do think some of my polling logic may have errors, because for device 3, the elapsed ms does not match, as there's a 600ms pause in between attempts, so device 3 should be > at least 600ms, but it's only 305ms. If i have more time I will figure out the issue.

        Id  Name                    Result          Attempts  Elapsed ms  Detail
        1   Core Router             OK                     1          68  21.5
        2   Backup Router           OK                     1        1204  19
 #      3   Lobby Camera 1          RETRIED-OK             3         305  22.1
        4   Lobby Camera 2          INVALID CONFIG         0           0  IpAddress is missing or blank
        5   Reception Display       SKIPPED                0           0  Device is disabled
        6   Server Room Sensor      INVALID CONFIG         0           0  Channel is negative
        7   Main Entrance Reader    INVALID CONFIG         0           0  PollIntervalMs is missing or not greater than 0
        8   O'Connor Wing Router    TIMEOUT                3        6023  max attempts reached :'(
        9   Rear Exit Camera        ERROR                  1          72  payload checksum mismatch
        10  Lobby Signage Board     BAD READING            1          41  reading is not a number