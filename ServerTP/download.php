<?php

$filepath = 'pdf/test.pdf';

header('Content-Type: application/pdf');

readfile($filepath);

?>