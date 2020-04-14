<?php

error_log('File upload api -------------------------------------');
error_log(print_r($_FILES, true));

$upload_dir = '/tmp/image/';
if (!file_exists($upload_dir)) {
    mkdir($upload_dir, 0777);
}
move_uploaded_file($_FILES['s_file']['tmp_name'], $upload_dir.$_FILES['s_file']['name']);

?>